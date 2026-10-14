"use client";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { api, ApiError, getApiBase } from "@/lib/api";
import { getToken, getUser, StoredUser, AUTH_CHANGE_EVENT } from "@/lib/auth";
import type { MeetingPlace, Place } from "@/types/place";
import {
  OriginItem,
  FriendWithLocation,
  COMMON_AREAS,
} from "@/components/meeting/GroupMeetingFinder";
import {
  Bot,
  Calendar,
  Car,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  Compass,
  Loader2,
  MapPin,
  Maximize2,
  Minimize2,
  MessageSquare,
  Plus,
  RotateCcw,
  Search,
  Send,
  Sparkles,
  UserCheck,
  Users,
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

interface MeetingChatMessage {
  id: string;
  sender: "user" | "ai";
  text: string;
  friendsAttached?: Array<{ name: string; avatarUrl?: string }>;
  steps?: ActivityStep[];
  meetingPlaces?: MeetingPlace[];
  origins?: OriginItem[];
  centroid?: { lat: number; lng: number } | null;
  aiInsight?: string | null;
  supportedAreas?: string[];
  timestamp: string;
}

const QUICK_PROMPTS_CHAT = [
  "Tìm quán cafe yên tĩnh làm việc",
  "Quán ăn ngon gần đây cho 4 người",
  "Quán trà sữa không gian rộng",
];

const QUICK_PROMPTS_MEETING = [
  "Nhóm 3 người: BV Chợ Rẫy, Chợ Bà Chiểu, SĐV Phú Nhuận, tìm công viên",
  "Tôi ở Chợ Bến Thành, bạn tôi ở Landmark 81, tìm quán cafe",
  "Nhóm 3 người xuất phát từ Q1, Q5, Bình Thạnh, tìm quán lẩu",
];

function removeVietnameseTones(str: string): string {
  return str
    .normalize("NFD")
    .replace(/[\u0300-\u036f]/g, "")
    .replace(/đ/g, "d")
    .replace(/Đ/g, "D")
    .toLowerCase();
}

function getDistanceKm(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const R = 6371;
  const dLat = ((lat2 - lat1) * Math.PI) / 180;
  const dLon = ((lon2 - lon1) * Math.PI) / 180;
  const a =
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos((lat1 * Math.PI) / 180) *
      Math.cos((lat2 * Math.PI) / 180) *
      Math.sin(dLon / 2) *
      Math.sin(dLon / 2);
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  return R * c;
}

interface Props {
  userLocation: { lat: number; lng: number } | null;
  onSelectPlaceOnMap?: (
    lat: number,
    lng: number,
    name: string,
    extra?: AttachedPlace
  ) => void;
  onOpenMeetingModal?: () => void;
  onResetMap?: () => void;
  onGroupSearchResults?: (
    results: MeetingPlace[],
    centroid: { lat: number; lng: number } | null,
    origins: Array<{ id: string; name: string; address: string; lat: number; lng: number }>
  ) => void;
  onSelectMeetingPlace?: (place: MeetingPlace, shouldCloseModal?: boolean) => void;
  onScheduleMeeting?: (place: Place, participants?: Array<{ name: string; email: string }>) => void;
  activeMeetingPlaceId?: string | null;
  isOpenControlled?: boolean;
  onCloseControlled?: () => void;
  controlledTab?: "chat" | "meeting";
  onTabChange?: (tab: "chat" | "meeting") => void;
  hideFloatingTrigger?: boolean;
}

function updateLiveSteps(prev: ActivityStep[], incoming: ActivityStep): ActivityStep[] {
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
  onResetMap,
  onGroupSearchResults,
  onSelectMeetingPlace,
  onScheduleMeeting,
  activeMeetingPlaceId,
  isOpenControlled,
  onCloseControlled,
  controlledTab,
  onTabChange,
  hideFloatingTrigger,
}: Props) {
  const [internalIsOpen, setInternalIsOpen] = useState(false);
  const isOpen = isOpenControlled !== undefined ? isOpenControlled : internalIsOpen;

  const [isMaximized, setIsMaximized] = useState(false);
  const [internalTab, setInternalTab] = useState<"chat" | "meeting">("chat");
  const activeTab = controlledTab !== undefined ? controlledTab : internalTab;

  const [currentUser, setCurrentUser] = useState<StoredUser | null>(() => {
    if (typeof window !== "undefined") {
      return getUser();
    }
    return null;
  });

  useEffect(() => {
    function handleAuthChange() {
      setCurrentUser(getUser());
    }
    window.addEventListener(AUTH_CHANGE_EVENT, handleAuthChange);
    return () => {
      window.removeEventListener(AUTH_CHANGE_EVENT, handleAuthChange);
    };
  }, []);

  // Friends directory state
  const [friends, setFriends] = useState<FriendWithLocation[]>([]);
  const [loadingFriends, setLoadingFriends] = useState(false);
  const [friendSearchQuery, setFriendSearchQuery] = useState("");
  const [showFriendDropdown, setShowFriendDropdown] = useState(false);
  const [showFriendSelector, setShowFriendSelector] = useState(false);
  const dropdownContainerRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (
        dropdownContainerRef.current &&
        !dropdownContainerRef.current.contains(event.target as Node)
      ) {
        setShowFriendDropdown(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => {
      document.removeEventListener("mousedown", handleClickOutside);
    };
  }, []);

  useEffect(() => {
    async function fetchFriends() {
      setLoadingFriends(true);
      try {
        const token = getToken();
        if (!token) {
          setFriends([]);
          return;
        }

        const res = await api.get<
          Array<{
            id: string;
            friend: {
              id: string;
              username: string;
              email: string;
              avatarUrl?: string;
            };
          }>
        >("/api/friends", token);

        const realFriends: FriendWithLocation[] = (res ?? []).map((item: any, idx) => {
          const friendObj = item.friend || item;
          const friendId = friendObj.id || item.friendId || item.id;
          const friendName =
            friendObj.username ||
            item.username ||
            friendObj.email?.split("@")[0] ||
            item.email?.split("@")[0] ||
            "Bạn";
          const friendEmail = friendObj.email || item.email || "";
          const friendAvatar = friendObj.avatarUrl || item.avatarUrl;
          const savedArea = COMMON_AREAS[idx % COMMON_AREAS.length];

          return {
            id: friendId,
            name: friendName,
            email: friendEmail,
            avatarUrl: friendAvatar,
            addressName: savedArea.name,
            lat: savedArea.lat,
            lng: savedArea.lng,
            selected: false,
          };
        });

        setFriends(realFriends);
      } catch (err: any) {
        if (err instanceof ApiError && err.status === 401) {
          setFriends([]);
        }
      } finally {
        setLoadingFriends(false);
      }
    }

    if (isOpen) {
      setCurrentUser(getUser());
      fetchFriends();
    }
  }, [isOpen]);

  const selectedFriends = friends.filter((f) => f.selected);

  const filteredFriends = friends.filter((f) => {
    if (f.selected) return false;
    const query = friendSearchQuery.trim().toLowerCase();
    if (!query) return true;
    const qNoTone = removeVietnameseTones(query);
    const nameLower = f.name.toLowerCase();
    const nameNoTone = removeVietnameseTones(f.name);
    const emailLower = f.email.toLowerCase();

    return (
      nameLower.includes(query) ||
      nameNoTone.includes(qNoTone) ||
      emailLower.includes(query)
    );
  });

  function addFriend(id: string) {
    setFriends((prev) =>
      prev.map((f) => (f.id === id ? { ...f, selected: true } : f))
    );
    setFriendSearchQuery("");
    setShowFriendDropdown(false);
  }

  function removeFriend(id: string) {
    setFriends((prev) =>
      prev.map((f) => (f.id === id ? { ...f, selected: false } : f))
    );
  }

  // --- TAB 1: Chat hỏi đáp địa điểm state ---
  const [messages, setMessages] = useState<ChatMessage[]>([
    {
      id: "welcome",
      sender: "ai",
      text: "Xin chào! Tôi là Trợ lý AI của LOSM. Bạn cần tìm địa điểm ăn uống, quán cafe họp nhóm hay lên lịch hẹn cùng bạn bè?",
      timestamp: "Vừa xong",
    },
  ]);
  const [input, setInput] = useState("");
  const [loading, setLoading] = useState(false);
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [liveSteps, setLiveSteps] = useState<ActivityStep[]>([]);
  const [expandedStepsMap, setExpandedStepsMap] = useState<Record<string, boolean>>({});

  // --- TAB 2: Chat tìm điểm hẹn nhóm state ---
  const [meetingMessages, setMeetingMessages] = useState<MeetingChatMessage[]>([
    {
      id: "meeting_welcome",
      sender: "ai",
      text: "Xin chào! Tôi là Trợ lý AI điều phối điểm hẹn nhóm. Hãy cho tôi biết nhóm bạn có bao nhiêu người, các điểm xuất phát ở đâu và bạn muốn tìm địa điểm nào (ví dụ: công viên, quán cafe, quán ăn...). Tôi sẽ tính toán ma trận lộ trình OSRM thực tế và đề xuất các điểm hẹn công bằng nhất cho cả nhóm!",
      timestamp: "Vừa xong",
    },
  ]);
  const [meetingInput, setMeetingInput] = useState("");
  const [meetingLoading, setMeetingLoading] = useState(false);
  const [meetingLiveSteps, setMeetingLiveSteps] = useState<ActivityStep[]>([]);

  const chatEndRef = useRef<HTMLDivElement>(null);
  const meetingEndRef = useRef<HTMLDivElement>(null);

  const chatTextareaRef = useRef<HTMLTextAreaElement>(null);
  const meetingTextareaRef = useRef<HTMLTextAreaElement>(null);

  const autoResizeTextarea = (el: HTMLTextAreaElement | null) => {
    if (!el) return;
    el.style.height = "auto";
    const nextHeight = Math.min(el.scrollHeight, 120);
    el.style.height = `${Math.max(nextHeight, 36)}px`;
  };

  useEffect(() => {
    autoResizeTextarea(chatTextareaRef.current);
  }, [input]);

  useEffect(() => {
    autoResizeTextarea(meetingTextareaRef.current);
  }, [meetingInput]);

  useEffect(() => {
    if (isOpen) {
      if (activeTab === "chat") {
        chatEndRef.current?.scrollIntoView({ behavior: "smooth" });
      } else {
        meetingEndRef.current?.scrollIntoView({ behavior: "smooth" });
      }
    }
  }, [messages, liveSteps, meetingMessages, meetingLiveSteps, isOpen, activeTab]);

  function handleTabSwitch(tab: "chat" | "meeting") {
    if (onTabChange) {
      onTabChange(tab);
    } else {
      setInternalTab(tab);
    }
  }

  function handleClose() {
    if (onCloseControlled) {
      onCloseControlled();
    } else {
      setInternalIsOpen(false);
    }
    setIsMaximized(false);
  }

  // Nút Tạo chat mới: Xóa tin nhắn, xóa form, và xóa toàn bộ điểm chấm trên bản đồ
  function handleNewChat() {
    if (activeTab === "chat") {
      setMessages([
        {
          id: "welcome",
          sender: "ai",
          text: "Xin chào! Tôi là Trợ lý AI của LOSM. Bạn cần tìm địa điểm ăn uống, quán cafe họp nhóm hay lên lịch hẹn cùng bạn bè?",
          timestamp: "Vừa xong",
        },
      ]);
      setSessionId(null);
      setInput("");
      setLiveSteps([]);
    } else {
      setMeetingMessages([
        {
          id: "meeting_welcome",
          sender: "ai",
          text: "Xin chào! Tôi là Trợ lý AI điều phối điểm hẹn nhóm. Hãy cho tôi biết nhóm bạn có bao nhiêu người, các điểm xuất phát ở đâu và bạn muốn tìm địa điểm nào. Tôi sẽ tính toán lộ trình OSRM thực tế và đề xuất các điểm hẹn công bằng nhất cho cả nhóm!",
          timestamp: "Vừa xong",
        },
      ]);
      setMeetingInput("");
      setMeetingLiveSteps([]);
      setFriends((prev) => prev.map((f) => ({ ...f, selected: false })));
      setShowFriendSelector(false);
    }

    onResetMap?.();
    toast.info("Đã tạo cuộc trò chuyện mới và xóa các điểm trên bản đồ");
  }

  // Gửi tin nhắn ở Tab 1: Hỏi đáp địa điểm
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

  // Gửi tin nhắn ở Tab 2: Tìm điểm hẹn nhóm (Tương tác dạng Chat + Show Live Steps AI)
  async function handleSendMeetingMessage(textToSend?: string) {
    const text = (textToSend ?? meetingInput).trim();
    if (!text || meetingLoading) return;

    const currentAttachedFriends = selectedFriends.map((f) => ({
      name: f.name,
      avatarUrl: f.avatarUrl,
    }));

    const userMsg: MeetingChatMessage = {
      id: `mu_${Date.now()}`,
      sender: "user",
      text,
      friendsAttached: currentAttachedFriends.length > 0 ? currentAttachedFriends : undefined,
      timestamp: new Date().toLocaleTimeString("vi-VN", {
        hour: "2-digit",
        minute: "2-digit",
      }),
    };

    setMeetingMessages((prev) => [...prev, userMsg]);
    setMeetingInput("");
    setMeetingLoading(true);

    // Khởi tạo các bước thực thi trực quan
    const initialSteps: ActivityStep[] = [
      {
        order: 1,
        kind: 0,
        toolName: "extract_members",
        summary: "Đang phân tích số lượng thành viên và các điểm xuất phát...",
        succeeded: false,
      },
    ];
    setMeetingLiveSteps(initialSteps);

    // Cập nhật bước giả lập mượt mà trong quá trình API chạy
    const timer1 = setTimeout(() => {
      setMeetingLiveSteps([
        {
          order: 1,
          kind: 1,
          toolName: "extract_members",
          summary: "Đã trích xuất thông tin thành viên và từ khoá địa điểm",
          succeeded: true,
        },
        {
          order: 2,
          kind: 0,
          toolName: "geocode_origins",
          summary: "Đang tra cứu toạ độ thực tế cho các vị trí xuất phát...",
          succeeded: false,
        },
      ]);
    }, 700);

    const timer2 = setTimeout(() => {
      setMeetingLiveSteps([
        {
          order: 1,
          kind: 1,
          toolName: "extract_members",
          summary: "Đã trích xuất thông tin thành viên và từ khoá địa điểm",
          succeeded: true,
        },
        {
          order: 2,
          kind: 1,
          toolName: "geocode_origins",
          summary: "Đã xác định toạ độ các vị trí xuất phát",
          succeeded: true,
        },
        {
          order: 3,
          kind: 0,
          toolName: "osrm_matrix",
          summary: "Đang tính toán ma trận lộ trình và thời gian OSRM thực tế...",
          succeeded: false,
        },
      ]);
    }, 1500);

    const myLat = userLocation?.lat ?? 10.7769;
    const myLng = userLocation?.lng ?? 106.7009;

    try {
      const token = getToken();

      const payload = {
        prompt: text,
        userLatitude: myLat,
        userLongitude: myLng,
        friends: selectedFriends.map((f) => ({
          name: f.name,
          latitude: f.lat,
          longitude: f.lng,
          avatarUrl: f.avatarUrl,
        })),
      };

      const res = await api.post<{
        query: string;
        centroid: { lat: number; lng: number };
        origins: OriginItem[];
        results: MeetingPlace[];
        aiInsight?: string;
        supportedAreas?: string[];
      }>("/api/agent/meeting-search", payload, token);

      clearTimeout(timer1);
      clearTimeout(timer2);

      const items = res?.results ?? [];
      const returnedOrigins: OriginItem[] = res?.origins ?? [];
      const centroidCoord = res?.centroid ? { lat: res.centroid.lat, lng: res.centroid.lng } : null;
      const supported = res?.supportedAreas ?? [];

      // Cập nhật bản đồ
      onGroupSearchResults?.(items, centroidCoord, returnedOrigins);
      if (items.length > 0) {
        onSelectMeetingPlace?.(items[0], false);
      }

      // Xây dựng danh sách bước thực thi tương ứng với kết quả
      const hasOrigins = returnedOrigins.length > 0;
      const completedSteps: ActivityStep[] = items.length > 0
        ? [
            {
              order: 1,
              kind: 1,
              toolName: "extract_members",
              summary: `Đã phân tích nhóm ${returnedOrigins.length} thành viên`,
              succeeded: true,
            },
            {
              order: 2,
              kind: 1,
              toolName: "geocode_origins",
              summary: `Đã tra cứu toạ độ thực tế của các điểm xuất phát`,
              succeeded: true,
            },
            {
              order: 3,
              kind: 1,
              toolName: "check_supported_areas",
              summary: `Đã kiểm tra khu vực hệ thống hỗ trợ (${supported.join(", ")})`,
              succeeded: true,
            },
            {
              order: 4,
              kind: 1,
              toolName: "osrm_matrix",
              summary: `Đã tính toán ma trận lộ trình & thời gian OSRM thực tế`,
              succeeded: true,
            },
            {
              order: 5,
              kind: 3,
              toolName: "finalize_meeting",
              summary: `Đã chọn ${items.length} điểm hẹn công bằng tối ưu nhất`,
              succeeded: true,
            },
          ]
        : [
            {
              order: 1,
              kind: 1,
              toolName: "extract_members",
              summary: `Đã phân tích yêu cầu điểm hẹn cho nhóm`,
              succeeded: true,
            },
            {
              order: 2,
              kind: 1,
              toolName: "geocode_origins",
              summary: hasOrigins
                ? `Đã tra cứu toạ độ các điểm xuất phát`
                : `Điểm xuất phát chưa có trong cơ sở dữ liệu hệ thống`,
              succeeded: hasOrigins,
            },
            {
              order: 3,
              kind: 1,
              toolName: "check_supported_areas",
              summary: `Khu vực hệ thống đang có dữ liệu: ${supported.length > 0 ? supported.join(", ") : "Quận 1"}`,
              succeeded: true,
            },
            {
              order: 4,
              kind: 3,
              toolName: "finalize_meeting",
              summary: hasOrigins
                ? `Không tìm thấy địa điểm phù hợp trong bán kính tìm kiếm`
                : `Cần xác nhận hoặc chọn lại địa điểm xuất phát`,
              succeeded: false,
            },
          ];

      const aiMsg: MeetingChatMessage = {
        id: `mai_${Date.now()}`,
        sender: "ai",
        text: res?.aiInsight || `Tôi đã tìm thấy ${items.length} điểm hẹn tối ưu nhất cho nhóm.`,
        steps: completedSteps,
        meetingPlaces: items,
        origins: returnedOrigins,
        centroid: centroidCoord,
        aiInsight: res?.aiInsight,
        supportedAreas: supported,
        timestamp: new Date().toLocaleTimeString("vi-VN", {
          hour: "2-digit",
          minute: "2-digit",
        }),
      };

      setMeetingMessages((prev) => [...prev, aiMsg]);

      if (items.length > 0) {
        toast.success(`Đã tìm thấy ${items.length} điểm hẹn tối ưu nhất cho nhóm!`);
      } else {
        toast.info("Không tìm thấy địa điểm phù hợp trong khu vực hỗ trợ.");
      }
    } catch (err) {
      clearTimeout(timer1);
      clearTimeout(timer2);
      console.error("Group meeting search error:", err);
      const msg =
        err instanceof ApiError
          ? err.message
          : "Có lỗi khi phân tích và tìm kiếm điểm hẹn. Vui lòng thử lại.";

      setMeetingMessages((prev) => [
        ...prev,
        {
          id: `merr_${Date.now()}`,
          sender: "ai",
          text: `Rất tiếc: ${msg}. Bạn hãy thử gửi yêu cầu khác nhé.`,
          timestamp: "Lỗi",
        },
      ]);
      toast.error(msg);
    } finally {
      setMeetingLoading(false);
      setMeetingLiveSteps([]);
    }
  }

  return (
    <>
      {/* Floating Trigger Button (khi đóng và không bị ẩn) */}
      {!isOpen && !hideFloatingTrigger && isOpenControlled === undefined && (
        <div className="fixed bottom-5 right-5 z-[1002] flex flex-col items-end">
          <Button
            onClick={() => {
              if (isOpenControlled !== undefined && onCloseControlled) {
                // controlled mode
              } else {
                setInternalIsOpen(true);
              }
            }}
            className="group flex h-11 items-center gap-2 rounded-full bg-card/95 border border-border px-4 text-foreground shadow-md hover:bg-muted active:scale-95 transition-all text-xs font-medium cursor-pointer"
          >
            <Bot className="h-4 w-4 text-primary" />
            <span>Hỏi Trợ lý AI</span>
          </Button>
        </div>
      )}

      {/* Cửa sổ Trợ lý AI (Hỗ trợ Full màn hình & Thu nhỏ góc phải) */}
      {isOpen && (
        <div
          className={
            isMaximized
              ? "fixed inset-0 z-[1050] w-screen h-screen bg-black/60 backdrop-blur-sm flex flex-col"
              : "fixed bottom-5 right-5 z-[1002] flex flex-col items-end"
          }
        >
          <Card
            className={
              isMaximized
                ? "w-full h-full rounded-none border-0 bg-background/98 backdrop-blur-xl shadow-2xl flex flex-col overflow-hidden"
                : "flex w-[calc(100vw-32px)] sm:w-[440px] h-[600px] max-h-[85vh] flex-col overflow-hidden rounded-2xl border bg-card/95 backdrop-blur-md shadow-2xl transition-all duration-300"
            }
          >
            {/* Header chung */}
            <CardHeader className="flex flex-row items-center justify-between border-b bg-muted/40 p-3.5 shrink-0">
              <div className="flex items-center gap-2.5">
                <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-gradient-to-tr from-blue-600 to-indigo-600 text-white shadow-xs">
                  <Bot className="h-4 w-4" />
                </div>
                <div>
                  <CardTitle className="text-sm font-bold flex items-center gap-1.5">
                    Trợ lý AI
                    <Badge variant="secondary" className="text-[10px] font-normal px-1.5 py-0 bg-blue-100 text-blue-700">
                      LOSM Agent
                    </Badge>
                  </CardTitle>
                  <p className="text-[10px] text-muted-foreground">
                    Gợi ý địa điểm & tính toán điểm hẹn nhóm công bằng
                  </p>
                </div>
              </div>

              {/* Action Buttons: Tạo chat mới, Phóng to/Thu nhỏ, Đóng */}
              <div className="flex items-center gap-1">
                {/* 1. Nút Tạo chat mới (Thay thế hoàn toàn nút Ẩn xuống) */}
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={handleNewChat}
                  title="Tạo cuộc trò chuyện mới (Xóa kết quả & điểm trên bản đồ)"
                  className="h-7 w-7 text-muted-foreground hover:text-primary hover:bg-primary/10 transition cursor-pointer"
                >
                  <RotateCcw className="h-3.5 w-3.5" />
                </Button>

                {/* 2. Nút Phóng to / Thu nhỏ Fullscreen */}
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={() => setIsMaximized(!isMaximized)}
                  title={isMaximized ? "Thu nhỏ về góc" : "Phóng to toàn màn hình"}
                  className="h-7 w-7 text-muted-foreground hover:text-foreground hover:bg-muted transition cursor-pointer"
                >
                  {isMaximized ? (
                    <Minimize2 className="h-3.5 w-3.5" />
                  ) : (
                    <Maximize2 className="h-3.5 w-3.5" />
                  )}
                </Button>

                {/* 3. Nút Đóng */}
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={handleClose}
                  title="Đóng Trợ lý AI"
                  className="h-7 w-7 text-muted-foreground hover:text-foreground hover:bg-muted transition cursor-pointer"
                >
                  <X className="h-3.5 w-3.5" />
                </Button>
              </div>
            </CardHeader>

            {/* Sub-Header: Tabs Navigation (Hỏi đáp địa điểm & Điểm hẹn nhóm) */}
            <div className="flex items-center gap-2 px-3.5 py-2 border-b bg-muted/20 shrink-0">
              <button
                type="button"
                onClick={() => handleTabSwitch("chat")}
                className={`flex items-center gap-1.5 px-3 py-1 rounded-lg text-xs font-semibold transition cursor-pointer ${
                  activeTab === "chat"
                    ? "bg-background text-foreground shadow-2xs border border-border"
                    : "text-muted-foreground hover:text-foreground hover:bg-muted/50"
                }`}
              >
                <MessageSquare className="h-3.5 w-3.5 text-primary" />
                <span>Hỏi đáp địa điểm</span>
              </button>
              <button
                type="button"
                onClick={() => handleTabSwitch("meeting")}
                className={`flex items-center gap-1.5 px-3 py-1 rounded-lg text-xs font-semibold transition cursor-pointer ${
                  activeTab === "meeting"
                    ? "bg-background text-foreground shadow-2xs border border-border"
                    : "text-muted-foreground hover:text-foreground hover:bg-muted/50"
                }`}
              >
                <Users className="h-3.5 w-3.5 text-primary" />
                <span>Tìm điểm hẹn nhóm</span>
              </button>
            </div>

            {/* TAB 1: TRÒ CHUYỆN HỎI ĐÁP ĐỊA ĐIỂM */}
            {activeTab === "chat" && (
              <div className="flex-1 flex flex-col min-h-0 overflow-hidden">
                <div
                  className={`flex-1 overflow-y-auto p-3.5 space-y-3.5 text-xs ${
                    isMaximized ? "max-w-3xl mx-auto w-full py-6" : ""
                  }`}
                >
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
                                      className="h-6 text-[10px] px-2 gap-1 bg-blue-600 hover:bg-blue-700 flex-1 cursor-pointer"
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
                  <div ref={chatEndRef} />
                </div>

                {/* Quick Prompt Suggestions */}
                {messages.length <= 3 && !loading && (
                  <div className={`flex items-center gap-1.5 overflow-x-auto px-3.5 py-1.5 border-t bg-muted/10 no-scrollbar ${isMaximized ? "max-w-3xl mx-auto w-full" : ""}`}>
                    {QUICK_PROMPTS_CHAT.map((prompt, idx) => (
                      <button
                        key={idx}
                        type="button"
                        onClick={() => void handleSendMessage(prompt)}
                        className="whitespace-nowrap text-[11px] rounded-full border bg-background px-2.5 py-1 hover:border-blue-500 hover:text-blue-600 transition shrink-0 cursor-pointer"
                      >
                        {prompt}
                      </button>
                    ))}
                  </div>
                )}

                {/* Input Footer */}
                <div className="border-t p-2.5 bg-background shrink-0">
                  <form
                    onSubmit={(e) => {
                      e.preventDefault();
                      void handleSendMessage();
                    }}
                    autoComplete="off"
                    className={`flex items-end gap-2 ${isMaximized ? "max-w-3xl mx-auto w-full" : ""}`}
                  >
                    <textarea
                      ref={chatTextareaRef}
                      name="chat_agent_query_no_autofill"
                      autoComplete="off"
                      autoCorrect="off"
                      autoCapitalize="off"
                      spellCheck={false}
                      data-lpignore="true"
                      data-form-type="other"
                      rows={1}
                      placeholder="Aa"
                      value={input}
                      onChange={(e) => setInput(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === "Enter" && !e.shiftKey) {
                          e.preventDefault();
                          void handleSendMessage();
                        }
                      }}
                      disabled={loading}
                      className="min-h-[36px] max-h-[120px] resize-none text-xs flex-1 bg-muted/30 py-2 px-3 rounded-xl border border-input focus:outline-none focus:ring-1 focus:ring-ring leading-normal overflow-y-auto no-scrollbar transition-[height] duration-75"
                    />
                    <Button
                      type="submit"
                      size="icon"
                      disabled={!input.trim() || loading}
                      className="h-9 w-9 shrink-0 rounded-xl bg-blue-600 hover:bg-blue-700 cursor-pointer self-end mb-[0.5px]"
                    >
                      <Send className="h-4 w-4" />
                    </Button>
                  </form>
                </div>
              </div>
            )}

            {/* TAB 2: TÌM ĐIỂM HẸN NHÓM (DẠNG CHAT TRÒ CHUYỆN + SHOW STEP AI CHẠY) */}
            {activeTab === "meeting" && (
              <div className="flex-1 flex flex-col min-h-0 overflow-hidden">
                <div
                  className={`flex-1 overflow-y-auto p-3.5 space-y-3.5 text-xs ${
                    isMaximized ? "max-w-4xl mx-auto w-full py-6" : ""
                  }`}
                >
                  {meetingMessages.map((msg) => (
                    <div
                      key={msg.id}
                      className={`flex flex-col ${
                        msg.sender === "user" ? "items-end" : "items-start"
                      }`}
                    >
                      <div
                        className={`max-w-[92%] rounded-2xl p-3 leading-relaxed ${
                          msg.sender === "user"
                            ? "bg-blue-600 text-white rounded-tr-xs"
                            : "bg-muted/70 text-foreground border rounded-tl-xs space-y-2.5"
                        }`}
                      >
                        {/* 1. Nếu tin nhắn user có gắn bạn bè */}
                        {msg.friendsAttached && msg.friendsAttached.length > 0 && (
                          <div className="flex flex-wrap gap-1 mb-1.5 pb-1.5 border-b border-white/20">
                            <span className="text-[10px] text-white/80 flex items-center gap-1 font-semibold">
                              <Users className="h-3 w-3" /> Đi cùng:
                            </span>
                            {msg.friendsAttached.map((fa, faIdx) => (
                              <span
                                key={faIdx}
                                className="inline-flex items-center gap-1 px-1.5 py-0.5 rounded-full bg-white/20 text-white text-[10px]"
                              >
                                {fa.name}
                              </span>
                            ))}
                          </div>
                        )}

                        {/* 2. Accordion các bước AI thực thi (Y hệt bên chat hỏi đáp địa điểm) */}
                        {msg.sender === "ai" && msg.steps && msg.steps.length > 0 && (
                          <div>
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
                              <span>{msg.steps.length} bước thực thi của AI</span>
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
                                    <span className="truncate">{st.summary}</span>
                                  </div>
                                ))}
                              </div>
                            )}
                          </div>
                        )}

                        {/* 3. Nhận xét của AI & Khu vực hỗ trợ */}
                        {msg.aiInsight && (
                          <div className="p-2.5 rounded-xl border border-primary/20 bg-primary/5 text-xs text-foreground space-y-1">
                            <div className="font-bold flex items-center gap-1.5 text-primary text-[11px]">
                              <Bot className="h-3.5 w-3.5" />
                              Trợ lý AI nhận xét:
                            </div>
                            <p className="text-muted-foreground leading-relaxed whitespace-pre-line text-[11px]">
                              {msg.aiInsight}
                            </p>
                          </div>
                        )}

                        {/* Nội dung text thông thường (nếu không có aiInsight) */}
                        {!msg.aiInsight && <p className="whitespace-pre-wrap">{msg.text}</p>}

                        {/* 4. Thẻ danh sách các địa điểm hẹn nhóm công bằng */}
                        {msg.meetingPlaces && msg.meetingPlaces.length > 0 && (
                          <div className="mt-2.5 pt-2 border-t border-border/60 space-y-2">
                            <div className="flex items-center justify-between">
                              <span className="text-[11px] font-bold text-foreground flex items-center gap-1">
                                <CheckCircle2 className="h-3.5 w-3.5 text-emerald-600" />
                                Địa điểm hẹn gợi ý ({msg.meetingPlaces.length}):
                              </span>
                              <span className="text-[10px] text-muted-foreground">
                                Tối ưu theo OSRM & độ công bằng
                              </span>
                            </div>

                            <div className="space-y-2">
                              {msg.meetingPlaces.map((item, idx) => {
                                const fairnessPercent = Math.round((item.fairnessScore ?? 0.8) * 100);
                                const isSelected = activeMeetingPlaceId === item.placeId;

                                return (
                                  <div
                                    key={item.placeId}
                                    onClick={() => onSelectMeetingPlace?.(item, false)}
                                    className={`rounded-xl border bg-background p-2.5 shadow-xs space-y-1.5 text-foreground transition cursor-pointer ${
                                      isSelected ? "border-primary bg-primary/5 ring-1 ring-primary/30" : "hover:bg-muted/40"
                                    }`}
                                  >
                                    <div className="flex items-start justify-between gap-2">
                                      <div className="flex items-center gap-1.5 min-w-0">
                                        <span className="flex h-4 w-4 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary font-bold text-[9px]">
                                          {idx + 1}
                                        </span>
                                        <h5 className="font-bold text-xs truncate">
                                          {item.name}
                                        </h5>
                                        {item.category && (
                                          <span className="text-[10px] text-muted-foreground truncate">
                                            • {item.category}
                                          </span>
                                        )}
                                      </div>
                                      <Badge variant="secondary" className="text-[9px] shrink-0 font-medium bg-emerald-500/10 text-emerald-600">
                                        Công bằng {fairnessPercent}%
                                      </Badge>
                                    </div>

                                    {item.address && (
                                      <p className="text-[10px] text-muted-foreground flex items-center gap-1 truncate">
                                        <MapPin className="h-3 w-3 shrink-0 text-muted-foreground" />
                                        <span className="truncate">{item.address}</span>
                                      </p>
                                    )}

                                    {/* Bảng khoảng cách & thời gian di chuyển từng người */}
                                    <div className="bg-muted/40 rounded-lg p-2 space-y-1 border border-border/50 text-[10px]">
                                      <div className="font-semibold text-muted-foreground text-[9px] uppercase tracking-wider flex items-center gap-1">
                                        <Car className="h-3 w-3 text-primary" />
                                        Khoảng cách & thời gian di chuyển từng người:
                                      </div>
                                      <div className="grid grid-cols-2 sm:grid-cols-3 gap-x-2 gap-y-1">
                                        {msg.origins && msg.origins.length > 0 ? (
                                          msg.origins.map((orig, oIdx) => {
                                            const dist =
                                              item.distanceKmByOrigin && item.distanceKmByOrigin[oIdx] !== undefined
                                                ? item.distanceKmByOrigin[oIdx]
                                                : getDistanceKm(orig.lat, orig.lng, item.latitude, item.longitude);
                                            const dur =
                                              item.durationMinutesByOrigin && item.durationMinutesByOrigin[oIdx] !== undefined
                                                ? item.durationMinutesByOrigin[oIdx]
                                                : (dist / 25) * 60;
                                            return (
                                              <div key={oIdx} className="flex items-center gap-1 text-muted-foreground truncate">
                                                <span className="font-semibold text-foreground truncate max-w-[65px]">
                                                  {orig.name}:
                                                </span>
                                                <span>
                                                  {dist.toFixed(1)} km
                                                  {dur !== undefined && dur !== null && (
                                                    <span className="ml-0.5 font-bold text-foreground">
                                                      ({Math.round(dur)}p)
                                                    </span>
                                                  )}
                                                </span>
                                              </div>
                                            );
                                          })
                                        ) : (
                                          <div className="flex items-center gap-1 text-muted-foreground truncate">
                                            <span className="font-semibold text-foreground">Khoảng cách TB:</span>
                                            <span>{(item.averageDistanceKm ?? 0).toFixed(1)} km</span>
                                          </div>
                                        )}
                                      </div>
                                    </div>

                                    {/* Action buttons */}
                                    <div className="flex items-center gap-1.5 pt-1">
                                      <Button
                                        variant="outline"
                                        size="sm"
                                        onClick={(e) => {
                                          e.stopPropagation();
                                          onSelectMeetingPlace?.(item, true);
                                          handleClose();
                                        }}
                                        className="h-6 text-[10px] px-2 gap-1 flex-1 cursor-pointer"
                                      >
                                        <Compass className="h-3 w-3" /> Xem bản đồ
                                      </Button>
                                      <Button
                                        size="sm"
                                        onClick={(e) => {
                                          e.stopPropagation();
                                          const invitees = msg.origins && msg.origins.length > 0
                                            ? msg.origins.map((o) => ({ name: o.name, email: o.address || "" }))
                                            : [
                                                ...(currentUser?.email
                                                  ? [{ name: currentUser.username || "Tôi", email: currentUser.email }]
                                                  : []),
                                                ...selectedFriends.map((f) => ({ name: f.name, email: f.email })),
                                              ];
                                          onScheduleMeeting?.(
                                            {
                                              id: item.placeId,
                                              name: item.name,
                                              address: item.address,
                                              latitude: item.latitude,
                                              longitude: item.longitude,
                                              category: item.category,
                                            },
                                            invitees
                                          );
                                        }}
                                        className="h-6 text-[10px] px-2 gap-1 bg-blue-600 hover:bg-blue-700 flex-1 cursor-pointer"
                                      >
                                        <Calendar className="h-3 w-3" /> Hẹn tại đây
                                      </Button>
                                    </div>
                                  </div>
                                );
                              })}
                            </div>
                          </div>
                        )}
                      </div>
                      <span className="text-[10px] text-muted-foreground mt-1 px-1">
                        {msg.timestamp}
                      </span>
                    </div>
                  ))}

                  {/* Hiển thị Live Steps trong lúc AI đang chạy (Y hệt tab hỏi đáp địa điểm) */}
                  {meetingLoading && (
                    <div className="flex flex-col items-start space-y-1 max-w-[88%] animate-in fade-in duration-200">
                      <div className="rounded-2xl p-3 bg-muted/60 border border-border text-foreground text-xs space-y-2 w-full shadow-xs">
                        <div className="flex items-center gap-2 font-semibold text-primary">
                          <Sparkles className="h-3.5 w-3.5 animate-pulse text-blue-500" />
                          <span>Trợ lý AI đang điều phối điểm hẹn nhóm...</span>
                        </div>

                        {meetingLiveSteps.length === 0 ? (
                          <div className="flex items-center gap-2 text-muted-foreground text-[11px] py-0.5">
                            <Loader2 className="h-3 w-3 animate-spin text-blue-600 shrink-0" />
                            <span>Đang phân tích yêu cầu điểm hẹn của bạn...</span>
                          </div>
                        ) : (
                          <div className="space-y-1.5 pt-1.5 border-t border-border/50 max-h-40 overflow-y-auto pr-1">
                            {meetingLiveSteps.map((step, sIdx) => {
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
                                    {step.summary}
                                  </span>
                                </div>
                              );
                            })}
                          </div>
                        )}
                      </div>
                    </div>
                  )}
                  <div ref={meetingEndRef} />
                </div>

                {/* Quick Prompts cho điểm hẹn nhóm */}
                {meetingMessages.length <= 2 && !meetingLoading && (
                  <div className={`flex items-center gap-1.5 overflow-x-auto px-3.5 py-1.5 border-t bg-muted/10 no-scrollbar ${isMaximized ? "max-w-4xl mx-auto w-full" : ""}`}>
                    {QUICK_PROMPTS_MEETING.map((prompt, idx) => (
                      <button
                        key={idx}
                        type="button"
                        onClick={() => void handleSendMeetingMessage(prompt)}
                        className="whitespace-nowrap text-[11px] rounded-full border bg-background px-2.5 py-1 hover:border-blue-500 hover:text-blue-600 transition shrink-0 cursor-pointer"
                      >
                        {prompt}
                      </button>
                    ))}
                  </div>
                )}

                {/* Companion Bar: Thêm bạn bè trong danh bạ trước khi gửi */}
                <div className={`border-t px-3 py-2 bg-muted/30 shrink-0 ${isMaximized ? "max-w-4xl mx-auto w-full" : ""}`}>
                  <div className="flex items-center justify-between gap-2">
                    <button
                      type="button"
                      onClick={() => setShowFriendSelector(!showFriendSelector)}
                      className="text-[11px] font-semibold text-muted-foreground hover:text-primary transition flex items-center gap-1 cursor-pointer"
                    >
                      <UserCheck className="h-3.5 w-3.5 text-primary" />
                      <span>Thêm bạn bè từ danh bạ</span>
                      {selectedFriends.length > 0 && (
                        <Badge variant="secondary" className="text-[9px] px-1 py-0 bg-primary/10 text-primary">
                          {selectedFriends.length} đã chọn
                        </Badge>
                      )}
                    </button>
                    <a
                      href="/profile/friends"
                      className="text-[10px] text-muted-foreground hover:text-primary transition"
                      title="Quản lý bạn bè"
                    >
                      + Quản lý bạn bè
                    </a>
                  </div>

                  {/* Thanh tìm kiếm & chip bạn bè mở rộng */}
                  {showFriendSelector && (
                    <div className="mt-2 space-y-2">
                      <div className="relative" ref={dropdownContainerRef}>
                        <div className="relative flex items-center">
                          <Search className="absolute left-2.5 h-3.5 w-3.5 text-muted-foreground pointer-events-none" />
                          <Input
                            type="text"
                            name="friend_search_query_chat_no_autofill"
                            autoComplete="off"
                            autoCorrect="off"
                            autoCapitalize="off"
                            spellCheck={false}
                            value={friendSearchQuery}
                            onChange={(e) => {
                              setFriendSearchQuery(e.target.value);
                              setShowFriendDropdown(true);
                            }}
                            onFocus={() => setShowFriendDropdown(true)}
                            placeholder="Gõ tên hoặc email bạn bè để thêm..."
                            className="h-8 pl-8 pr-7 text-xs rounded-xl bg-background border-border"
                          />
                          {friendSearchQuery && (
                            <button
                              type="button"
                              onClick={() => {
                                setFriendSearchQuery("");
                                setShowFriendDropdown(false);
                              }}
                              className="absolute right-2 text-muted-foreground hover:text-foreground p-0.5"
                            >
                              <X className="h-3 w-3" />
                            </button>
                          )}
                        </div>

                        {showFriendDropdown && (
                          <div className="absolute left-0 right-0 bottom-full mb-1.5 z-50 max-h-48 overflow-y-auto rounded-xl border border-border bg-popover text-popover-foreground shadow-2xl p-1.5 space-y-1">
                            {loadingFriends ? (
                              <div className="flex items-center justify-center py-2.5 text-xs text-muted-foreground gap-2">
                                <Loader2 className="h-3.5 w-3.5 animate-spin text-primary" />
                                Đang tải danh bạ...
                              </div>
                            ) : friends.length === 0 ? (
                              <div className="py-2.5 px-3 text-center text-xs text-muted-foreground space-y-1">
                                <p>Danh bạ hiện đang trống.</p>
                                <a
                                  href="/profile/friends"
                                  className="text-primary hover:underline text-[11px] inline-block font-medium"
                                >
                                  + Đến trang Quản lý bạn bè
                                </a>
                              </div>
                            ) : filteredFriends.length === 0 ? (
                              <div className="py-2 px-3 text-center text-[11px] text-muted-foreground">
                                Không tìm thấy bạn bè khớp với &quot;{friendSearchQuery}&quot;.
                              </div>
                            ) : (
                              filteredFriends.map((friend) => (
                                <div
                                  key={friend.id}
                                  onClick={() => addFriend(friend.id)}
                                  className="flex items-center justify-between p-1.5 rounded-lg hover:bg-muted/70 cursor-pointer transition text-left"
                                >
                                  <div className="flex items-center gap-2 min-w-0">
                                    {friend.avatarUrl ? (
                                      <img
                                        src={
                                          friend.avatarUrl.startsWith("http")
                                            ? `/api/avatar?url=${encodeURIComponent(friend.avatarUrl)}`
                                            : friend.avatarUrl
                                        }
                                        alt={friend.name}
                                        className="h-5 w-5 rounded-full object-cover shrink-0"
                                      />
                                    ) : (
                                      <div className="flex h-5 w-5 items-center justify-center rounded-full bg-primary/10 text-primary font-bold text-[9px] shrink-0">
                                        {friend.name.charAt(0).toUpperCase()}
                                      </div>
                                    )}
                                    <div className="min-w-0">
                                      <div className="text-xs font-semibold text-foreground truncate">
                                        {friend.name}
                                      </div>
                                      <div className="text-[9px] text-muted-foreground truncate">
                                        {friend.email}
                                      </div>
                                    </div>
                                  </div>
                                  <Plus className="h-3.5 w-3.5 text-muted-foreground shrink-0 ml-2" />
                                </div>
                              ))
                            )}
                          </div>
                        )}
                      </div>

                      {/* Chips bạn bè đã chọn */}
                      {selectedFriends.length > 0 && (
                        <div className="flex flex-wrap gap-1 pt-0.5">
                          {selectedFriends.map((f) => (
                            <span
                              key={f.id}
                              className="inline-flex items-center gap-1 pl-1.5 pr-2 py-0.5 rounded-full bg-primary/10 text-primary text-[11px] font-medium border border-primary/20 shadow-2xs"
                            >
                              <span className="truncate max-w-[110px]">{f.name}</span>
                              <button
                                type="button"
                                onClick={() => removeFriend(f.id)}
                                className="hover:text-destructive transition ml-0.5 cursor-pointer"
                                title="Xóa bạn này"
                              >
                                <X className="h-3 w-3" />
                              </button>
                            </span>
                          ))}
                        </div>
                      )}
                    </div>
                  )}
                </div>

                {/* Input Footer cho Điểm hẹn nhóm */}
                <div className="border-t p-2.5 bg-background shrink-0">
                  <form
                    onSubmit={(e) => {
                      e.preventDefault();
                      void handleSendMeetingMessage();
                    }}
                    autoComplete="off"
                    className={`flex items-end gap-2 ${isMaximized ? "max-w-4xl mx-auto w-full" : ""}`}
                  >
                    <textarea
                      ref={meetingTextareaRef}
                      name="group_meeting_chat_query_no_autofill"
                      autoComplete="off"
                      autoCorrect="off"
                      autoCapitalize="off"
                      spellCheck={false}
                      data-lpignore="true"
                      data-form-type="other"
                      rows={1}
                      placeholder="Aa"
                      value={meetingInput}
                      onChange={(e) => setMeetingInput(e.target.value)}
                      onKeyDown={(e) => {
                        if (e.key === "Enter" && !e.shiftKey) {
                          e.preventDefault();
                          void handleSendMeetingMessage();
                        }
                      }}
                      disabled={meetingLoading}
                      className="min-h-[36px] max-h-[120px] resize-none text-xs flex-1 bg-muted/30 py-2 px-3 rounded-xl border border-input focus:outline-none focus:ring-1 focus:ring-ring leading-normal overflow-y-auto no-scrollbar transition-[height] duration-75"
                    />
                    <Button
                      type="submit"
                      size="icon"
                      disabled={!meetingInput.trim() || meetingLoading}
                      className="h-9 w-9 shrink-0 rounded-xl bg-blue-600 hover:bg-blue-700 cursor-pointer self-end mb-[0.5px]"
                    >
                      <Send className="h-4 w-4" />
                    </Button>
                  </form>
                </div>
              </div>
            )}
          </Card>
        </div>
      )}
    </>
  );
}
