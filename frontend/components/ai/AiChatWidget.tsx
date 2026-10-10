"use client";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { api, ApiError } from "@/lib/api";
import { getToken } from "@/lib/auth";
import {
  Bot,
  Calendar,
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
  onSelectPlaceOnMap?: (lat: number, lng: number, name: string) => void;
  onOpenMeetingModal?: () => void;
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

  const messagesEndRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (isOpen && !isMinimized) {
      messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
    }
  }, [messages, isOpen, isMinimized]);

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

    try {
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

      const res = await api.post<{
        sessionId?: string;
        answer: string;
        attachedPlaces?: AttachedPlace[];
      }>("/api/agent", body, getToken());

      if (res?.sessionId) {
        setSessionId(res.sessionId);
      }

      const aiMsg: ChatMessage = {
        id: `ai_${Date.now()}`,
        sender: "ai",
        text: res?.answer || "Tôi đã tìm kiếm các địa điểm phù hợp theo yêu cầu của bạn.",
        attachedPlaces: res?.attachedPlaces || [],
        timestamp: new Date().toLocaleTimeString("vi-VN", {
          hour: "2-digit",
          minute: "2-digit",
        }),
      };

      setMessages((prev) => [...prev, aiMsg]);
    } catch (err) {
      const errorMsg =
        err instanceof ApiError ? err.message : "Đã xảy ra lỗi kết nối với trợ lý AI.";
      setMessages((prev) => [
        ...prev,
        {
          id: `err_${Date.now()}`,
          sender: "ai",
          text: `Rất tiếc: ${errorMsg}. Vui lòng thử lại câu hỏi khác.`,
          timestamp: "Lỗi",
        },
      ]);
    } finally {
      setLoading(false);
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
                                      )
                                    }
                                    className="h-6 text-[10px] px-2 gap-1 flex-1"
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
                  <div className="flex items-center gap-2 text-muted-foreground text-xs p-2 bg-muted/30 rounded-xl w-fit">
                    <Loader2 className="h-3.5 w-3.5 animate-spin text-blue-600" />
                    <span>Trợ lý AI đang suy nghĩ và tìm kiếm...</span>
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
