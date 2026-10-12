"use client";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { api, ApiError, getApiBase } from "@/lib/api";
import { getToken } from "@/lib/auth";
import {
  Bot,
  Calendar,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  Compass,
  CornerDownLeft,
  Loader2,
  MapPin,
  MessageSquare,
  Minus,
  Send,
  Sparkles,
  X,
} from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";

export interface ActivityStep {
  order: number;
  kind: number;
  toolName?: string | null;
  summary: string;
  succeeded: boolean;
}

interface AttachedPlace {
  id?: string;
  name?: string;
  address?: string;
  latitude?: number;
  longitude?: number;
  category?: string;
}

interface ChatMessage {
  id: string;
  sender: "user" | "ai";
  text: string;
  steps?: ActivityStep[];
  attachedPlaces?: AttachedPlace[];
  timestamp: string;
}

const QUICK_PROMPTS = [
  "Tìm quán cafe yên tĩnh làm việc",
  "Quán ăn ngon gần đây cho 4 người",
  "Quán trà sữa không gian rộng",
];

interface Props {
  userLocation: { lat: number; lng: number } | null;
  onSelectPlaceOnMap?: (
    lat: number,
    lng: number,
    name: string,
    extra?: AttachedPlace
  ) => void;
  onOpenMeetingModal?: () => void;
}

function updateLiveSteps(prev: ActivityStep[], incoming: ActivityStep): ActivityStep[] {
  // 1. ToolCall (kind === 0): công cụ bắt đầu chạy
  if (incoming.kind === 0) {
    const sameToolSteps = prev.filter((p) => p.toolName === incoming.toolName);
    const retryCount = sameToolSteps.length;
    let label = incoming.summary;
    if (retryCount > 0) {
      if (incoming.toolName === "geocode_place") {
        label = `Đang thử lại tra cứu toạ độ (lần ${retryCount + 1})...`;
      } else if (incoming.toolName === "search_places") {
        label = `Đang thử lại tìm kiếm với phạm vi rộng hơn...`;
      } else {
        label = `Đang thử lại ${incoming.summary.toLowerCase()}...`;
      }
    }
    return [
      ...prev,
      {
        ...incoming,
        summary: label,
      },
    ];
  }

  // 2. ToolResult (kind === 1): công cụ vừa chạy xong -> CẬP NHẬT TẠI CHỖ dòng ToolCall tương ứng
  if (incoming.kind === 1) {
    let targetIdx = -1;
    for (let i = prev.length - 1; i >= 0; i--) {
      if (prev[i].toolName === incoming.toolName && prev[i].kind === 0) {
        targetIdx = i;
        break;
      }
    }

    if (targetIdx !== -1) {
      const updated = [...prev];
      const prevStep = updated[targetIdx];
      const wasRetry = prevStep.summary.includes("thử lại");
      let summary = incoming.summary;
      if (wasRetry) {
        summary = incoming.succeeded
          ? `${incoming.summary} (sau khi thử lại)`
          : `Chưa tìm thấy toạ độ, chuẩn bị thử lại...`;
      }

      updated[targetIdx] = {
        ...prevStep,
        kind: incoming.kind,
        summary,
        succeeded: incoming.succeeded,
      };
      return updated;
    }

    return [...prev, incoming];
  }

  // 3. Finalize (kind === 3): hoàn tất
  if (incoming.kind === 3) {
    return [
      ...prev,
      {
        ...incoming,
        summary: incoming.summary || "Hoàn tất gợi ý địa điểm",
      },
    ];
  }

  return [...prev, incoming];
}

function cleanStepsForMessage(rawSteps: ActivityStep[]): ActivityStep[] {
  if (!rawSteps || rawSteps.length === 0) return [];

  const toolCountMap: Record<string, number> = {};
  for (const s of rawSteps) {
    const t = s.toolName || "system";
    toolCountMap[t] = (toolCountMap[t] || 0) + 1;
  }

  const toolResults: ActivityStep[] = [];
  const seenTools = new Set<string>();

  for (let i = rawSteps.length - 1; i >= 0; i--) {
    const s = rawSteps[i];
    if (s.kind === 1 || s.kind === 3) {
      const t = s.toolName || "system";
      if (!seenTools.has(t)) {
        seenTools.add(t);
        const count = toolCountMap[t] || 1;
        let summary = s.summary.replace(/^Đang\s+/i, "Đã ");
        if (count > 2 && s.kind === 1) {
          const retries = Math.floor(count / 2);
          if (retries > 1) {
            summary = `${summary} (qua ${retries} lần tìm kiếm)`;
          }
        }
        toolResults.unshift({
          ...s,
          summary,
        });
      }
    }
  }

  if (toolResults.length > 0) {
    return toolResults;
  }

  const fallbackList: ActivityStep[] = [];
  const seenSummaries = new Set<string>();
  for (const s of rawSteps) {
    const cleaned = s.summary.replace(/^Đang\s+/i, "Đã ");
    if (!seenSummaries.has(cleaned)) {
      seenSummaries.add(cleaned);
      fallbackList.push({ ...s, summary: cleaned, succeeded: true });
    }
  }
  return fallbackList;
}

export default function AiChatWidget({
  userLocation,
  onSelectPlaceOnMap,
  onOpenMeetingModal,
}: Props) {
  const [isOpen, setIsOpen] = useState(false);
  const [isMinimized, setIsMinimized] = useState(false);
  const [messages, setMessages] = useState<ChatMessage[]>([
    {
      id: "welcome",
      sender: "ai",
      text: "Xin chào! Tôi là Trợ lý AI của LOSM. Bạn cần tìm địa điểm ăn uống, quán cafe họp nhóm hay lên lịch hẹn công bằng cùng bạn bè?",
      timestamp: "Vừa xong",
    },
  ]);
  const [input, setInput] = useState("");
  const [loading, setLoading] = useState(false);
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [liveSteps, setLiveSteps] = useState<ActivityStep[]>([]);
  const [expandedStepsMap, setExpandedStepsMap] = useState<Record<string, boolean>>({});

  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (isOpen && !isMinimized) {
      messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
    }
  }, [messages, liveSteps, isOpen, isMinimized]);

  async function handleSendMessage(textToSend?: string) {
    const text = (textToSend ?? input).trim();
    if (!text || loading) return;

    const userMsg: ChatMessage = {
      id: `u_${Date.now()}`,
      sender: "user",
      text,
      timestamp: new Date().toLocaleTimeString("vi-VN", {
        hour: "2-digit",
        minute: "2-digit",
      }),
    };

    setMessages((prev) => [...prev, userMsg]);
    setInput("");
    setLoading(true);
    setLiveSteps([]);

    const lat = userLocation?.lat ?? 10.7769;
    const lng = userLocation?.lng ?? 106.7009;

    const body: {
      message: string;
      latitude: number;
      longitude: number;
      sessionId?: string;
    } = {
      message: text,
      latitude: lat,
      longitude: lng,
    };

    if (sessionId) {
      body.sessionId = sessionId;
    }

    try {
      const token = getToken();
      const streamUrl = `${getApiBase()}/api/agent/stream`;
      const response = await fetch(streamUrl, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...(token ? { Authorization: `Bearer ${token}` } : {}),
        },
        body: JSON.stringify(body),
      });

      if (!response.ok || !response.body) {
        throw new Error(`HTTP ${response.status}`);
      }

      const reader = response.body.getReader();
      const decoder = new TextDecoder();
      let buffer = "";
      let currentSteps: ActivityStep[] = [];
      let finalResult: {
        sessionId?: string;
        answer?: string;
        steps?: ActivityStep[];
        attachedPlaces?: AttachedPlace[];
      } | null = null;
      let streamError: string | null = null;

      while (true) {
        const { done, value } = await reader.read();
        if (done) break;

        buffer += decoder.decode(value, { stream: true });
        const lines = buffer.split("\n");
        buffer = lines.pop() ?? "";

        let currentEvent = "";
        for (let i = 0; i < lines.length; i++) {
          const line = lines[i].trim();
          if (line.startsWith("event:")) {
            currentEvent = line.substring(6).trim();
          } else if (line.startsWith("data:") && currentEvent) {
            const jsonStr = line.substring(5).trim();
            try {
              const data = JSON.parse(jsonStr);
              if (currentEvent === "step") {
                const step = data as ActivityStep;
                currentSteps = updateLiveSteps(currentSteps, step);
                setLiveSteps([...currentSteps]);
              } else if (currentEvent === "result") {
                finalResult = data;
              } else if (currentEvent === "error") {
                streamError = data.message || "Đã xảy ra lỗi";
              }
            } catch (err) {
              console.warn("Parse SSE event error:", err);
            }
          }
        }
      }

      if (streamError && !finalResult) {
        throw new Error(streamError);
      }

      if (finalResult?.sessionId) {
        setSessionId(finalResult.sessionId);
      }

      const answerText =
        finalResult?.answer ||
        "Tôi đã tìm kiếm các địa điểm phù hợp theo yêu cầu của bạn.";

      const finalSteps = cleanStepsForMessage(
        currentSteps.length > 0 ? currentSteps : finalResult?.steps || []
      );

      const aiMsg: ChatMessage = {
        id: `ai_${Date.now()}`,
        sender: "ai",
        text: answerText,
        steps: finalSteps,
        attachedPlaces: finalResult?.attachedPlaces || [],
        timestamp: new Date().toLocaleTimeString("vi-VN", {
          hour: "2-digit",
          minute: "2-digit",
        }),
      };

      setMessages((prev) => [...prev, aiMsg]);
    } catch (err) {
      console.warn("Streaming agent error, falling back to sync:", err);
      try {
        const res = await api.post<{
          sessionId?: string;
          answer: string;
          steps?: ActivityStep[];
          attachedPlaces?: AttachedPlace[];
        }>("/api/agent", body, getToken());

        if (res?.sessionId) {
          setSessionId(res.sessionId);
        }

        const aiMsg: ChatMessage = {
          id: `ai_${Date.now()}`,
          sender: "ai",
          text: res?.answer || "Tôi đã tìm kiếm các địa điểm phù hợp theo yêu cầu của bạn.",
          steps: cleanStepsForMessage(res?.steps || []),
          attachedPlaces: res?.attachedPlaces || [],
          timestamp: new Date().toLocaleTimeString("vi-VN", {
            hour: "2-digit",
            minute: "2-digit",
          }),
        };

        setMessages((prev) => [...prev, aiMsg]);
      } catch (fallbackErr) {
        const errorMsg =
          fallbackErr instanceof ApiError
            ? fallbackErr.message
            : "Đã xảy ra lỗi kết nối với trợ lý AI.";
        setMessages((prev) => [
          ...prev,
          {
            id: `err_${Date.now()}`,
            sender: "ai",
            text: `Rất tiếc: ${errorMsg}. Vui lòng thử lại câu hỏi khác.`,
            timestamp: "Lỗi",
          },
        ]);
      }
    } finally {
      setLoading(false);
      setLiveSteps([]);
    }
  }

  return (
    <div className="fixed bottom-5 right-5 z-[1002] flex flex-col items-end">
      {/* Floating Trigger Button */}
      {!isOpen && (
        <Button
          onClick={() => {
            setIsOpen(true);
            setIsMinimized(false);
          }}
          className="group flex h-11 items-center gap-2 rounded-full bg-card/95 border border-border px-4 text-foreground shadow-md hover:bg-muted active:scale-95 transition-all text-xs font-medium"
        >
          <MessageSquare className="h-4 w-4 text-muted-foreground" />
          <span>Hỏi Trợ lý AI</span>
        </Button>
      )}

      {/* Chat Window */}
      {isOpen && (
        <Card
          className={`flex w-[calc(100vw-32px)] sm:w-[390px] flex-col overflow-hidden rounded-2xl border bg-card/95 backdrop-blur-md shadow-2xl transition-all duration-300 ${
            isMinimized ? "h-14" : "h-[540px] max-h-[82vh]"
          }`}
        >
          {/* Header */}
          <CardHeader className="flex flex-row items-center justify-between border-b bg-muted/40 p-3.5">
            <div className="flex items-center gap-2.5">
              <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-linear-to-tr from-blue-600 to-indigo-600 text-white shadow-xs">
                <Bot className="h-4 w-4" />
              </div>
              <div>
                <CardTitle className="text-sm font-bold flex items-center gap-1.5">
                  Trợ lý AI
                  <Badge variant="secondary" className="text-[10px] font-normal px-1.5 py-0 bg-blue-100 text-blue-700">
                    LOSM Agent
                  </Badge>
                </CardTitle>
                {!isMinimized && (
                  <p className="text-[10px] text-muted-foreground">
                    Sẵn sàng gợi ý địa điểm & hỗ trợ lên lịch
                  </p>
                )}
              </div>
            </div>

            <div className="flex items-center gap-1">
              <Button
                variant="ghost"
                size="icon"
                onClick={() => setIsMinimized(!isMinimized)}
                className="h-7 w-7 text-muted-foreground hover:text-foreground"
              >
                <Minus className="h-3.5 w-3.5" />
              </Button>
              <Button
                variant="ghost"
                size="icon"
                onClick={() => setIsOpen(false)}
                className="h-7 w-7 text-muted-foreground hover:text-foreground"
              >
                <X className="h-3.5 w-3.5" />
              </Button>
            </div>
          </CardHeader>

          {/* Chat Body */}
          {!isMinimized && (
            <>
              <div className="flex-1 overflow-y-auto p-3.5 space-y-3.5 text-xs">
                {messages.map((msg) => (
                  <div
                    key={msg.id}
                    className={`flex flex-col ${
                      msg.sender === "user" ? "items-end" : "items-start"
                    }`}
                  >
                    <div
                      className={`max-w-[85%] rounded-2xl p-3 leading-relaxed ${
                        msg.sender === "user"
                          ? "bg-blue-600 text-white rounded-tr-xs"
                          : "bg-muted/70 text-foreground border rounded-tl-xs"
                      }`}
                    >
                      {/* Execution Steps Accordion */}
                      {msg.sender === "ai" && msg.steps && msg.steps.length > 0 && (
                        <div className="mb-2">
                          <button
                            type="button"
                            onClick={() =>
                              setExpandedStepsMap((prev) => ({
                                ...prev,
                                [msg.id]: !prev[msg.id],
                              }))
                            }
                            className="inline-flex items-center gap-1.5 text-[10px] font-medium text-muted-foreground hover:text-foreground bg-background/80 hover:bg-background border border-border/60 rounded-md px-2 py-0.5 transition shadow-2xs cursor-pointer"
                          >
                            <Sparkles className="h-3 w-3 text-blue-500" />
                            <span>{msg.steps.length} bước thực thi</span>
                            {expandedStepsMap[msg.id] ? (
                              <ChevronDown className="h-3 w-3" />
                            ) : (
                              <ChevronRight className="h-3 w-3" />
                            )}
                          </button>
                          {expandedStepsMap[msg.id] && (
                            <div className="mt-1.5 space-y-1 p-2 rounded-lg bg-background/60 border border-border/50 text-[10px] text-muted-foreground animate-in fade-in">
                              {msg.steps.map((st, sIdx) => (
                                <div key={sIdx} className="flex items-center gap-1.5">
                                  <span className="text-emerald-500 font-bold">✓</span>
                                  <span className="truncate">{st.summary || st.toolName || "Đã xử lý"}</span>
                                </div>
                              ))}
                            </div>
                          )}
                        </div>
                      )}

                      <p className="whitespace-pre-wrap">{msg.text}</p>

                      {/* Attached Places Cards */}
                      {msg.attachedPlaces && msg.attachedPlaces.length > 0 && (
                        <div className="mt-2.5 pt-2 border-t border-border/60 space-y-2">
                          <p className="text-[11px] font-semibold text-muted-foreground">
                            Địa điểm gợi ý:
                          </p>
                          {msg.attachedPlaces.map((place, pIdx) => (
                            <div
                              key={pIdx}
                              className="rounded-xl border bg-background p-2.5 shadow-xs space-y-1.5 text-foreground"
                            >
                              <div className="flex items-start justify-between gap-2">
                                <h5 className="font-bold text-xs truncate">
                                  {place.name}
                                </h5>
                                {place.category && (
                                  <Badge variant="outline" className="text-[10px] px-1 py-0 shrink-0">
                                    {place.category}
                                  </Badge>
                                )}
                              </div>
                              {place.address && (
                                <p className="text-[11px] text-muted-foreground line-clamp-1">
                                  {place.address}
                                </p>
                              )}
                              <div className="flex items-center gap-1.5 pt-1">
                                {place.latitude && place.longitude && onSelectPlaceOnMap && (
                                  <Button
                                    variant="outline"
                                    size="sm"
                                    onClick={() =>
                                      onSelectPlaceOnMap(
                                        place.latitude!,
                                        place.longitude!,
                                        place.name ?? "Địa điểm",
                                        place
                                      )
                                    }
                                    className="h-6 text-[10px] px-2 gap-1 flex-1 cursor-pointer"
                                  >
                                    <Compass className="h-3 w-3" /> Xem bản đồ
                                  </Button>
                                )}
                                {onOpenMeetingModal && (
                                  <Button
                                    size="sm"
                                    onClick={onOpenMeetingModal}
                                    className="h-6 text-[10px] px-2 gap-1 bg-blue-600 hover:bg-blue-700 flex-1"
                                  >
                                    <Calendar className="h-3 w-3" /> Hẹn tại đây
                                  </Button>
                                )}
                              </div>
                            </div>
                          ))}
                        </div>
                      )}
                    </div>
                    <span className="text-[10px] text-muted-foreground mt-1 px-1">
                      {msg.timestamp}
                    </span>
                  </div>
                ))}

                {loading && (
                  <div className="flex flex-col items-start space-y-1 max-w-[88%] animate-in fade-in duration-200">
                    <div className="rounded-2xl p-3 bg-muted/60 border border-border text-foreground text-xs space-y-2 w-full shadow-xs">
                      <div className="flex items-center gap-2 font-semibold text-primary">
                        <Sparkles className="h-3.5 w-3.5 animate-pulse text-blue-500" />
                        <span>Trợ lý AI đang thực thi...</span>
                      </div>

                      {liveSteps.length === 0 ? (
                        <div className="flex items-center gap-2 text-muted-foreground text-[11px] py-0.5">
                          <Loader2 className="h-3 w-3 animate-spin text-blue-600 shrink-0" />
                          <span>Đang phân tích yêu cầu của bạn...</span>
                        </div>
                      ) : (
                        <div className="space-y-1.5 pt-1.5 border-t border-border/50 max-h-40 overflow-y-auto pr-1">
                          {liveSteps.map((step, sIdx) => {
                            const isRunning = step.kind === 0;
                            return (
                              <div
                                key={sIdx}
                                className="flex items-center gap-2 text-[11px] transition-all"
                              >
                                {isRunning ? (
                                  <Loader2 className="h-3 w-3 animate-spin text-blue-600 shrink-0" />
                                ) : step.succeeded ? (
                                  <span className="flex h-3.5 w-3.5 items-center justify-center rounded-full bg-emerald-500/10 text-emerald-600 font-bold text-[10px] shrink-0">
                                    ✓
                                  </span>
                                ) : (
                                  <span className="flex h-3.5 w-3.5 items-center justify-center rounded-full bg-amber-500/10 text-amber-600 font-bold text-[10px] shrink-0">
                                    !
                                  </span>
                                )}
                                <span
                                  className={`truncate ${
                                    isRunning
                                      ? "font-medium text-foreground"
                                      : "text-muted-foreground"
                                  }`}
                                >
                                  {step.summary || (step.toolName ? `Đang chạy ${step.toolName}` : "Đang xử lý...")}
                                </span>
                              </div>
                            );
                          })}
                        </div>
                      )}
                    </div>
                  </div>
                )}
                <div ref={messagesEndRef} />
              </div>

              {/* Quick Prompt Suggestions */}
              {messages.length <= 3 && !loading && (
                <div className="flex items-center gap-1.5 overflow-x-auto px-3.5 py-1.5 border-t bg-muted/10 no-scrollbar">
                  {QUICK_PROMPTS.map((prompt, idx) => (
                    <button
                      key={idx}
                      type="button"
                      onClick={() => void handleSendMessage(prompt)}
                      className="whitespace-nowrap text-[11px] rounded-full border bg-background px-2.5 py-1 hover:border-blue-500 hover:text-blue-600 transition shrink-0"
                    >
                      {prompt}
                    </button>
                  ))}
                </div>
              )}

              {/* Input Footer */}
              <div className="border-t p-2.5 bg-background">
                <form
                  onSubmit={(e) => {
                    e.preventDefault();
                    void handleSendMessage();
                  }}
                  className="flex items-center gap-2"
                >
                  <Input
                    placeholder="Nhập câu hỏi (ví dụ: Tìm quán cafe yên tĩnh...)"
                    value={input}
                    onChange={(e) => setInput(e.target.value)}
                    disabled={loading}
                    className="h-9 text-xs flex-1 bg-muted/30"
                  />
                  <Button
                    type="submit"
                    size="icon"
                    disabled={!input.trim() || loading}
                    className="h-9 w-9 shrink-0 bg-blue-600 hover:bg-blue-700"
                  >
                    <Send className="h-4 w-4" />
                  </Button>
                </form>
              </div>
            </>
          )}
        </Card>
      )}
    </div>
  );
}
