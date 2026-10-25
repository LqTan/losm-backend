"use client";

import TransitResults from "@/components/ai/TransitResults";
import {
  type TransitChatResult,
  type TransitChatJourney,
  type TransitChatLocation,
  type TransitChatStop,
} from "@/lib/transit-chat";
import { fetchPedestrianStreetRoute, fetchBusStreetRoute } from "@/lib/transit-routing";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { api, ApiError } from "@/lib/api";
import { getToken, getUser, StoredUser, AUTH_CHANGE_EVENT } from "@/lib/auth";
import type { MeetingPlace, Place } from "@/types/place";
import {
  OriginItem,
  FriendWithLocation,
  COMMON_AREAS,
} from "@/components/meeting/GroupMeetingFinder";
import {
  ArrowDownWideNarrow,
  Bot,
  Calendar,
  Car,
  CheckCircle2,
  ChevronDown,
  ChevronRight,
  Compass,
  Cpu,
  Loader2,
  MapPin,
  Maximize2,
  Minimize2,
  Navigation2,
  Plus,
  RotateCcw,
  Scale,
  Search,
  Send,
  Sparkles,
  Target,
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

export interface AttachedPlace {
  id?: string;
  placeId?: string;
  name?: string;
  address?: string;
  latitude?: number;
  longitude?: number;
  category?: string;
}

import { formatRouteNumber } from "@/lib/transit";

export interface TransitRouteDetails {
  origin?: { name: string; latitude: number; longitude: number };
  destination?: { name: string; latitude: number; longitude: number };
  boardingStop: { id: string; name: string; address?: string; latitude: number; longitude: number };
  alightingStop: { id: string; name: string; address?: string; latitude: number; longitude: number };
  walkOriginToBoarding?: [number, number][];
  transitCoordinates: [number, number][];
  walkAlightingToDest?: [number, number][];
  intermediateStops?: Array<{ id: string; name: string; latitude: number; longitude: number }>;
  routeNumber?: string | number;
  routeName?: string;
  stopCount?: number;
}

export interface AttachedRoute {
  originName: string;
  originLatitude: number;
  originLongitude: number;
  destinationName: string;
  destinationLatitude: number;
  destinationLongitude: number;
  memberName?: string | null;
  distanceKm: number;
  durationMinutes: number;
  summary?: string | null;
  coordinates: [number, number][];
  transitDetails?: TransitRouteDetails;
}

export interface ChatMessage {
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
  attachedRoute?: AttachedRoute;
  transitResults?: TransitChatResult[];
  attachedPlaces?: AttachedPlace[];
  rankingMethod?: string;
  timestamp: string;
}

const QUICK_PROMPTS = [
  "Tìm trạm xe buýt gần tôi",
  "Tuyến xe buýt 01 đi qua những trạm nào?",
  "Đi xe buýt từ Bến Thành đến Bến Bạch Đằng",
  "Tìm quán cafe yên tĩnh làm việc",
  "Nhóm 3 người: BV Chợ Rẫy, Chợ Bà Chiểu, SĐV Phú Nhuận",
  "Tôi ở Chợ Bến Thành, bạn tôi ở Landmark 81, tìm quán cafe",
  "Quán ăn ngon gần đây cho 4 người",
  "Chỉ đường từ Bến Thành đến Diamond Plaza",
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

function formatStepSummary(step: ActivityStep): string {
  const toolName = step.toolName;
  if (!toolName) {
    if (step.summary === "Response ready") return "Đã hoàn thành câu trả lời";
    if (step.summary === "Thinking") return "Đang phân tích và lập kế hoạch";
    return step.summary || "Đã xử lý xong";
  }

  const toolDisplayMap: Record<string, { call: string; result: string }> = {
    search_transit_stops: {
      call: "Đang tra cứu các trạm xe buýt/metro...",
      result: "Đã tìm thấy các trạm xe buýt/metro phù hợp",
    },
    get_transit_routes: {
      call: "Đang tải thông tin tuyến xe buýt/metro...",
      result: "Đã tải thông tin lộ trình tuyến xe",
    },
    find_transit_journey: {
      call: "Đang kiểm tra các tuyến xe buýt/metro kết nối trực tiếp...",
      result: "Đã tìm thấy hành trình xe buýt/metro phù hợp",
    },
    search_places: {
      call: "Đang tìm kiếm các địa điểm trong khu vực...",
      result: "Đã tìm thấy các địa điểm phù hợp",
    },
    search_meeting_places: {
      call: "Đang tính toán toạ độ và tìm điểm hẹn nhóm công bằng...",
      result: "Đã xác định các điểm hẹn nhóm tối ưu",
    },
    get_directions: {
      call: "Đang tính toán tuyến đường di chuyển...",
      result: "Đã tính toán xong lộ trình di chuyển",
    },
    geocode_place: {
      call: "Đang xác định toạ độ địa danh...",
      result: "Đã xác định toạ độ địa danh",
    },
    place_reviews: {
      call: "Đang đọc đánh giá của địa điểm...",
      result: "Đã tải đánh giá của địa điểm",
    },
    get_supported_areas: {
      call: "Đang kiểm tra các khu vực hệ thống hỗ trợ...",
      result: "Đã xác nhận danh sách khu vực hỗ trợ",
    },
    flag_invalid_place: {
      call: "Đang đánh dấu địa điểm sai lệch dữ liệu...",
      result: "Đã gắn cờ địa điểm sai lệch toạ độ",
    },
  };

  const entry = toolDisplayMap[toolName];
  if (entry) {
    if (step.kind === 0) return entry.call;
    return step.succeeded ? entry.result : `${entry.result} (thất bại)`;
  }

  return step.summary || `Công cụ: ${toolName}`;
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
  onSelectOriginRoute?: (place: MeetingPlace, origin: OriginItem) => void;
  onScheduleMeeting?: (place: Place, participants?: Array<{ name: string; email: string }>) => void;
  onRouteReceived?: (route: AttachedRoute) => void;
  activeMeetingPlaceId?: string | null;
  isOpenControlled?: boolean;
  onCloseControlled?: () => void;
  hideFloatingTrigger?: boolean;
}

export default function AiChatWidget({
  userLocation,
  onSelectPlaceOnMap,
  onOpenMeetingModal,
  onResetMap,
  onGroupSearchResults,
  onSelectMeetingPlace,
  onSelectOriginRoute,
  onScheduleMeeting,
  onRouteReceived,
  activeMeetingPlaceId,
  isOpenControlled,
  onCloseControlled,
  hideFloatingTrigger,
}: Props) {
  const [internalIsOpen, setInternalIsOpen] = useState(false);
  const isOpen = isOpenControlled !== undefined ? isOpenControlled : internalIsOpen;
  const [isMaximized, setIsMaximized] = useState(false);

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

  // Unified Chat State
  const [messages, setMessages] = useState<ChatMessage[]>([
    {
      id: "welcome",
      sender: "ai",
      text: "Xin chào! Tôi là Trợ lý AI của LOSM. Bạn có thể hỏi gợi ý địa điểm cá nhân, tìm điểm hẹn nhóm công bằng chỉ đường, tìm trạm xe buýt/metro hoặc tra tuyến công cộng. Hãy nhập yêu cầu của bạn!",
      timestamp: "Vừa xong",
    },
  ]);
  const [input, setInput] = useState("");
  const [sessionId, setSessionId] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);
  const [liveSteps, setLiveSteps] = useState<ActivityStep[]>([]);
  const [expandedStepsMap, setExpandedStepsMap] = useState<Record<string, boolean>>({});

  const chatEndRef = useRef<HTMLDivElement>(null);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const autoResizeTextarea = (el: HTMLTextAreaElement | null) => {
    if (!el) return;
    el.style.height = "auto";
    const nextHeight = Math.min(el.scrollHeight, 120);
    el.style.height = `${Math.max(nextHeight, 36)}px`;
  };

  useEffect(() => {
    autoResizeTextarea(textareaRef.current);
  }, [input]);

  useEffect(() => {
    if (isOpen) {
      chatEndRef.current?.scrollIntoView({ behavior: "smooth" });
    }
  }, [messages, liveSteps, isOpen]);

  function handleClose() {
    if (onCloseControlled) {
      onCloseControlled();
    } else {
      setInternalIsOpen(false);
    }
    setIsMaximized(false);
  }

  // Nút Tạo chat mới: Xóa tin nhắn, xóa form và xóa các điểm trên bản đồ
  function handleNewChat() {
    setSessionId(null);
    setMessages([
      {
        id: "welcome",
        sender: "ai",
        text: "Xin chào! Tôi là Trợ lý AI của LOSM. Bạn có thể hỏi gợi ý địa điểm cá nhân, tìm điểm hẹn nhóm công bằng chỉ đường, tìm trạm xe buýt/metro hoặc tra tuyến công cộng. Hãy nhập yêu cầu của bạn!",
        timestamp: "Vừa xong",
      },
    ]);
    setInput("");
    setLiveSteps([]);
    setFriends((prev) => prev.map((f) => ({ ...f, selected: false })));
    setShowFriendSelector(false);
    onResetMap?.();
    toast.info("Đã tạo cuộc trò chuyện mới và xóa các điểm trên bản đồ");
  }

  // Gửi tin nhắn hợp nhất: Gợi ý địa điểm cá nhân, Tìm điểm hẹn nhóm công bằng & Chỉ đường
  async function handleSendMessage(textToSend?: string) {
    const text = (textToSend ?? input).trim();
    if (!text || loading) return;

    const currentAttachedFriends = selectedFriends.map((f) => ({
      name: f.name,
      avatarUrl: f.avatarUrl,
    }));

    const userMsg: ChatMessage = {
      id: `u_${Date.now()}`,
      sender: "user",
      text,
      friendsAttached: currentAttachedFriends.length > 0 ? currentAttachedFriends : undefined,
      timestamp: new Date().toLocaleTimeString("vi-VN", {
        hour: "2-digit",
        minute: "2-digit",
      }),
    };

    setMessages((prev) => [...prev, userMsg]);
    setInput("");
    setLoading(true);

    setLiveSteps([
      {
        order: 1,
        kind: 0,
        toolName: "analyze_intent",
        summary: "Đang phân tích yêu cầu và mục tiêu...",
        succeeded: false,
      },
    ]);

    const timer1 = setTimeout(() => {
      setLiveSteps([
        {
          order: 1,
          kind: 1,
          toolName: "analyze_intent",
          summary: "Đã phân tích mục tiêu & ý định",
          succeeded: true,
        },
        {
          order: 2,
          kind: 0,
          toolName: "query_data",
          summary: "Đang tra cứu dữ liệu địa điểm & mạng lưới giao thông...",
          succeeded: false,
        },
      ]);
    }, 600);

    const timer2 = setTimeout(() => {
      setLiveSteps([
        {
          order: 1,
          kind: 1,
          toolName: "analyze_intent",
          summary: "Đã phân tích mục tiêu & ý định",
          succeeded: true,
        },
        {
          order: 2,
          kind: 1,
          toolName: "query_data",
          summary: "Đã kết nối dữ liệu địa điểm & mạng lưới giao thông",
          succeeded: true,
        },
        {
          order: 3,
          kind: 0,
          toolName: "find_solution",
          summary: "Đang tính toán tối ưu kết quả và lộ trình...",
          succeeded: false,
        },
      ]);
    }, 1400);

    const myLat = userLocation?.lat ?? 10.7769;
    const myLng = userLocation?.lng ?? 106.7009;

    try {
      const token = getToken();
      const friendsContext = selectedFriends.length
        ? `\nSelected group members (context only): ${JSON.stringify(
            selectedFriends.map((f) => ({
              name: f.name,
              latitude: f.lat,
              longitude: f.lng,
            }))
          )}`
        : "";

      const res = await api.post<{
        sessionId: string;
        answer: string;
        steps: ActivityStep[];
        attachedPlaces?: AttachedPlace[];
        attachedRoute?: AttachedRoute;
        transitResults?: TransitChatResult[];
        meetingResults?: {
          query: string;
          centroid?: { latitude: number; longitude: number };
          searchRadiusKm?: number;
          rankingMethod?: string;
          origins?: Array<{ name?: string; latitude: number; longitude: number; displayName?: string }>;
          results?: Array<{
            placeId: string;
            name: string;
            address?: string;
            latitude: number;
            longitude: number;
            category?: string;
            openingHours?: string;
            distanceKmByOrigin?: number[];
            averageDistanceKm?: number;
            maxDistanceKm?: number;
            spreadKm?: number;
            relevanceScore?: number;
            distanceScore?: number;
            fairnessScore?: number;
            finalScore?: number;
            durationMinutesByOrigin?: number[];
            averageDurationMinutes?: number;
            spreadMinutes?: number;
            isRoadRouting?: boolean;
            isOnnxScored?: boolean;
          }>;
        };
      }>(
        "/api/agent",
        {
          message: text + friendsContext,
          latitude: myLat,
          longitude: myLng,
          sessionId,
        },
        token
      );

      clearTimeout(timer1);
      clearTimeout(timer2);

      setSessionId(res.sessionId);

      // 1. Tuyến đường di chuyển (Directions)
      if (res.attachedRoute) {
        onRouteReceived?.(res.attachedRoute);
      }

      // 2. Điểm hẹn nhóm (Search Meeting Places)
      let meetingItems: MeetingPlace[] = [];
      let centroidCoord: { lat: number; lng: number } | null = null;
      let returnedOrigins: OriginItem[] = [];

      if (res.meetingResults?.results && res.meetingResults.results.length > 0) {
        meetingItems = res.meetingResults.results.map((r) => ({
          placeId: r.placeId,
          name: r.name,
          address: r.address || null,
          latitude: r.latitude,
          longitude: r.longitude,
          category: r.category || null,
          openingHours: r.openingHours || null,
          distanceKmByOrigin: r.distanceKmByOrigin || [],
          averageDistanceKm: r.averageDistanceKm || 0,
          maxDistanceKm: r.maxDistanceKm || 0,
          spreadKm: r.spreadKm || 0,
          relevanceScore: r.relevanceScore || 1,
          distanceScore: r.distanceScore || 1,
          fairnessScore: r.fairnessScore || 1,
          finalScore: r.finalScore || 1,
          durationMinutesByOrigin: r.durationMinutesByOrigin,
          averageDurationMinutes: r.averageDurationMinutes,
          spreadMinutes: r.spreadMinutes,
          isRoadRouting: r.isRoadRouting ?? true,
          isOnnxScored: r.isOnnxScored ?? false,
        })).sort((a, b) => (b.finalScore ?? 0) - (a.finalScore ?? 0));

        centroidCoord = res.meetingResults.centroid
          ? { lat: res.meetingResults.centroid.latitude, lng: res.meetingResults.centroid.longitude }
          : null;

        returnedOrigins = (res.meetingResults.origins || []).map((o, idx) => ({
          id: `origin_${idx}`,
          name: o.name || `Người ${idx + 1}`,
          address: o.displayName || o.name || "Điểm xuất phát",
          lat: o.latitude,
          lng: o.longitude,
          avatarUrl: selectedFriends[idx]?.avatarUrl,
          isMe: idx === 0,
        }));

        onGroupSearchResults?.(meetingItems, centroidCoord, returnedOrigins);
      }

      // 3. Phương tiện công cộng (Transit)
      const tResults = res.transitResults ?? [];
      const allJourneys = tResults.flatMap((r) => r.journeys || []);
      const allRoutes = tResults.flatMap((r) => r.routes || []);
      const allStops = tResults.flatMap((r) => r.stops || []);

      // 4. Các bước xử lý của Agent (Dựa trên kết quả thực tế đã tìm thấy)
      const completedSteps: ActivityStep[] = [];

      completedSteps.push({
        order: 1,
        kind: 1,
        toolName: "analyze_intent",
        summary: `Đã phân tích yêu cầu: "${text}"`,
        succeeded: true,
      });

      if (allJourneys.length > 0) {
        completedSteps.push({
          order: 2,
          kind: 1,
          toolName: "query_transit",
          summary: "Đã tra cứu dữ liệu mạng lưới trạm & tuyến xe buýt/metro nội bộ",
          succeeded: true,
        });
        completedSteps.push({
          order: 3,
          kind: 3,
          toolName: "find_transit_journey",
          summary: `Đã tìm thấy ${allJourneys.length} hành trình xe buýt/metro trực tiếp`,
          succeeded: true,
        });
      } else if (allRoutes.length > 0) {
        completedSteps.push({
          order: 2,
          kind: 1,
          toolName: "query_transit",
          summary: "Đã tra cứu dữ liệu mạng lưới tuyến xe buýt/metro nội bộ",
          succeeded: true,
        });
        completedSteps.push({
          order: 3,
          kind: 3,
          toolName: "get_transit_routes",
          summary: `Đã tải thông tin ${allRoutes.length} tuyến xe buýt/metro`,
          succeeded: true,
        });
      } else if (allStops.length > 0) {
        completedSteps.push({
          order: 2,
          kind: 1,
          toolName: "query_transit",
          summary: "Đã tra cứu dữ liệu trạm dừng xe buýt/metro nội bộ",
          succeeded: true,
        });
        completedSteps.push({
          order: 3,
          kind: 3,
          toolName: "search_transit_stops",
          summary: `Đã tìm thấy ${allStops.length} trạm dừng phù hợp`,
          succeeded: true,
        });
      } else if (res.attachedRoute) {
        completedSteps.push({
          order: 2,
          kind: 1,
          toolName: "geocode_locations",
          summary: `Đã xác định toạ độ: ${res.attachedRoute.originName} -> ${res.attachedRoute.destinationName}`,
          succeeded: true,
        });
        completedSteps.push({
          order: 3,
          kind: 3,
          toolName: "get_directions",
          summary: `Đã tính toán lộ trình: ${res.attachedRoute.distanceKm.toFixed(1)} km (~${Math.round(res.attachedRoute.durationMinutes)} phút)`,
          succeeded: true,
        });
      } else if (meetingItems.length > 0) {
        completedSteps.push({
          order: 2,
          kind: 1,
          toolName: "geocode_origins",
          summary: `Đã xác định toạ độ các điểm xuất phát của nhóm (${returnedOrigins.length} thành viên)`,
          succeeded: true,
        });
        completedSteps.push({
          order: 3,
          kind: 3,
          toolName: "search_meeting_places",
          summary: `Đã chọn ${meetingItems.length} điểm hẹn nhóm công bằng tối ưu nhất`,
          succeeded: true,
        });
      } else if (res.attachedPlaces && res.attachedPlaces.length > 0) {
        completedSteps.push({
          order: 2,
          kind: 1,
          toolName: "search_places",
          summary: "Đã tra cứu các địa điểm trong khu vực hoạt động",
          succeeded: true,
        });
        completedSteps.push({
          order: 3,
          kind: 3,
          toolName: "finalize_places",
          summary: `Đã tìm thấy ${res.attachedPlaces.length} địa điểm phù hợp`,
          succeeded: true,
        });
      } else {
        completedSteps.push({
          order: 2,
          kind: 3,
          toolName: "finalize",
          summary: "Đã hoàn thành phản hồi yêu cầu",
          succeeded: true,
        });
      }

      const stepsToDisplay: ActivityStep[] =
        res.steps && res.steps.length > 0
          ? res.steps
              .filter((s) => s.kind === 0 || s.kind === 1 || s.kind === 3)
              .map((s) => ({
                ...s,
                summary: formatStepSummary(s),
              }))
          : completedSteps;

      const newAiId = `ai_${Date.now()}`;
      const aiMsg: ChatMessage = {
        id: newAiId,
        sender: "ai",
        text: res.answer,
        steps: stepsToDisplay,
        attachedPlaces: res.attachedPlaces,
        attachedRoute: res.attachedRoute,
        transitResults: res.transitResults,
        meetingPlaces: meetingItems.length > 0 ? meetingItems : undefined,
        origins: returnedOrigins.length > 0 ? returnedOrigins : undefined,
        centroid: centroidCoord,
        rankingMethod:
          res.meetingResults?.rankingMethod ||
          (meetingItems.some((p) => p.isOnnxScored) ? "onnx" : "cradis-dn4"),
        timestamp: new Date().toLocaleTimeString("vi-VN", {
          hour: "2-digit",
          minute: "2-digit",
        }),
      };

      setMessages((prev) => [...prev, aiMsg]);
      setExpandedStepsMap((prev) => ({ ...prev, [newAiId]: true }));

      if (allJourneys.length > 0 || allRoutes.length > 0 || allStops.length > 0) {
        toast.success("Đã tìm thấy thông tin phương tiện công cộng!");
      } else if (res.attachedRoute) {
        toast.success("Đã tìm thấy tuyến đường và hiển thị lên bản đồ!");
      } else if (meetingItems.length > 0) {
        toast.success(`Đã tìm thấy ${meetingItems.length} địa điểm gặp mặt phù hợp!`);
      } else if (res.attachedPlaces && res.attachedPlaces.length > 0) {
        toast.success(`Đã tìm thấy ${res.attachedPlaces.length} địa điểm phù hợp!`);
      }
    } catch (err) {
      console.error("AI Assistant error:", err);
      const msg =
        err instanceof ApiError
          ? err.message
          : "Có lỗi khi phân tích và xử lý yêu cầu. Vui lòng thử lại.";

      setMessages((prev) => [
        ...prev,
        {
          id: `err_${Date.now()}`,
          sender: "ai",
          text: `Rất tiếc: ${msg}. Bạn hãy thử gửi yêu cầu khác nhé.`,
          timestamp: "Lỗi",
        },
      ]);
      toast.error(msg);
    } finally {
      clearTimeout(timer1);
      clearTimeout(timer2);
      setLoading(false);
      setLiveSteps([]);
    }
  }

  return (
    <>
      {/* Floating Trigger Button (khi đóng và không bị ẩn) */}
      {!isOpen && !hideFloatingTrigger && isOpenControlled === undefined && (
        <div className="fixed bottom-5 right-5 z-[1002] flex flex-col items-end">
          <Button
            onClick={() => setInternalIsOpen(true)}
            className="group flex h-11 items-center gap-2 rounded-full bg-card/95 border border-border px-4 text-foreground shadow-md hover:bg-muted active:scale-95 transition-all text-xs font-medium cursor-pointer"
          >
            <Bot className="h-4 w-4 text-primary" />
            <span>Trợ lý AI</span>
          </Button>
        </div>
      )}

      {/* Cửa sổ Trợ lý AI duy nhất (Hỗ trợ Full màn hình & Thu nhỏ góc phải) */}
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
            {/* Header duy nhất */}
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
                    Địa điểm, điểm hẹn, chỉ đường & bus/metro
                  </p>
                </div>
              </div>

              {/* Action Buttons: Tạo chat mới, Phóng to/Thu nhỏ, Đóng */}
              <div className="flex items-center gap-1">
                <Button
                  variant="ghost"
                  size="icon"
                  onClick={handleNewChat}
                  title="Tạo cuộc trò chuyện mới (Xóa kết quả & điểm trên bản đồ)"
                  className="h-7 w-7 text-muted-foreground hover:text-primary hover:bg-primary/10 transition cursor-pointer"
                >
                  <RotateCcw className="h-3.5 w-3.5" />
                </Button>

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

            {/* Khung chat hợp nhất */}
            <div className="flex-1 flex flex-col min-h-0 overflow-hidden">
              <div
                className={`flex-1 overflow-y-auto p-3.5 space-y-3.5 text-xs ${
                  isMaximized ? "max-w-4xl mx-auto w-full py-6" : ""
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

                      {/* 2. Accordion các bước AI thực thi */}
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

                      {/* 3. Nhận xét của AI */}
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

                      {/* Thẻ gợi ý địa điểm: chỉ hiển thị khi là tra cứu địa điểm, không hiển thị đè lên kết quả phương tiện công cộng hay tìm đường */}
                      {!msg.transitResults?.some((r) => (r.journeys && r.journeys.length > 0) || (r.routes && r.routes.length > 0) || (r.stops && r.stops.length > 0)) &&
                        !msg.attachedRoute &&
                        !msg.meetingPlaces?.length &&
                        msg.attachedPlaces?.map((place, index) => (
                        <div key={place.placeId ?? index} className="rounded-xl border p-2.5 space-y-1">
                          <p className="font-medium">{place.name}</p>
                          <p className="text-muted-foreground">{place.address}</p>
                          {onSelectPlaceOnMap && place.latitude != null && place.longitude != null && (
                            <div className="pt-1.5 border-t border-border/40">
                              <Button
                                size="sm"
                                variant="outline"
                                onClick={() => {
                                  onSelectPlaceOnMap(
                                    place.latitude!,
                                    place.longitude!,
                                    place.name ?? "",
                                    { ...place, id: place.placeId }
                                  );
                                  toast.success(`Đã hiển thị "${place.name}" lên bản đồ!`);
                                  handleClose();
                                }}
                                className="w-full h-7 text-[11px] font-medium gap-1.5 text-blue-600 hover:text-blue-700 border-blue-200 bg-blue-50/50 hover:bg-blue-100/60 dark:text-blue-400 dark:border-blue-800 dark:bg-blue-950/40 cursor-pointer justify-center rounded-lg shadow-2xs"
                              >
                                <Compass className="h-3.5 w-3.5 text-primary" /> Xem trên bản đồ
                              </Button>
                            </div>
                          )}
                        </div>
                      ))}

                      {msg.transitResults && (() => {
                        const allJourneys = (msg.transitResults ?? []).flatMap((r) => r.journeys);

                        const triggerJourneyRoute = async (
                          journey: TransitChatJourney,
                          origin?: TransitChatLocation | null,
                          destination?: TransitChatLocation | null
                        ) => {
                          if (!onRouteReceived) return;

                          const origLat =
                            origin?.latitude ?? userLocation?.lat ?? journey.boardingStop.latitude;
                          const origLng =
                            origin?.longitude ?? userLocation?.lng ?? journey.boardingStop.longitude;
                          const origName =
                            origin?.name && origin.name !== "Origin" && origin.name !== "Location"
                              ? origin.name
                              : userLocation
                              ? "Vị trí của bạn (GPS)"
                              : "Điểm xuất phát";

                          const destLat =
                            destination?.latitude ?? journey.alightingStop.latitude;
                          const destLng =
                            destination?.longitude ?? journey.alightingStop.longitude;
                          const destName =
                            destination?.name &&
                            destination.name !== "Destination" &&
                            destination.name !== "Origin" &&
                            destination.name !== "Location"
                              ? destination.name
                              : "Điểm đến";

                          const rawBusStops: [number, number][] =
                            journey.route.stops && journey.route.stops.length > 0
                              ? journey.route.stops.map((s: TransitChatStop) => [s.latitude, s.longitude] as [number, number])
                              : [
                                  [journey.boardingStop.latitude, journey.boardingStop.longitude],
                                  [journey.alightingStop.latitude, journey.alightingStop.longitude],
                                ];

                          toast.loading("Đang tính toán đường đi theo mạng lưới đường phố...", { id: "transit-routing" });

                          // Tải lộ trình đi bộ và lộ trình xe buýt bám sát đường phố thực tế qua OSRM
                          const [walkOriginToBoarding, transitCoords, walkAlightingToDest] = await Promise.all([
                            fetchPedestrianStreetRoute(origLat, origLng, journey.boardingStop.latitude, journey.boardingStop.longitude),
                            fetchBusStreetRoute(rawBusStops),
                            fetchPedestrianStreetRoute(journey.alightingStop.latitude, journey.alightingStop.longitude, destLat, destLng),
                          ]);

                          toast.dismiss("transit-routing");

                          const allCoords: [number, number][] = [
                            [origLat, origLng],
                            ...transitCoords,
                            [destLat, destLng],
                          ];

                          const distKm =
                            (journey.accessDistanceMeters + journey.egressDistanceMeters) / 1000 +
                            transitCoords.length * 0.4;
                          const durMin = Math.max(5, Math.round(distKm * 3.5));

                          const transitRoute: AttachedRoute = {
                            originName: origName,
                            originLatitude: origLat,
                            originLongitude: origLng,
                            destinationName: destName,
                            destinationLatitude: destLat,
                            destinationLongitude: destLng,
                            memberName: null,
                            distanceKm: distKm,
                            durationMinutes: durMin,
                            summary: `Tuyến ${formatRouteNumber(journey.route.number)} · ${journey.route.name}`,
                            coordinates: allCoords,
                            transitDetails: {
                              origin: { name: origName, latitude: origLat, longitude: origLng },
                              destination: { name: destName, latitude: destLat, longitude: destLng },
                              boardingStop: {
                                id: String(journey.boardingStop.id),
                                name: journey.boardingStop.name,
                                address: journey.boardingStop.address,
                                latitude: journey.boardingStop.latitude,
                                longitude: journey.boardingStop.longitude,
                              },
                              alightingStop: {
                                id: String(journey.alightingStop.id),
                                name: journey.alightingStop.name,
                                address: journey.alightingStop.address,
                                latitude: journey.alightingStop.latitude,
                                longitude: journey.alightingStop.longitude,
                              },
                              walkOriginToBoarding,
                              transitCoordinates: transitCoords,
                              walkAlightingToDest,
                              intermediateStops: (journey.route.stops || []).slice(1, -1).map((s: TransitChatStop) => ({
                                id: String(s.id),
                                name: s.name,
                                latitude: s.latitude,
                                longitude: s.longitude,
                              })),
                              routeNumber: formatRouteNumber(journey.route.number),
                              routeName: journey.route.name,
                              stopCount: journey.stopCount,
                            },
                          };

                          onRouteReceived(transitRoute);
                          toast.success(
                            `Đã hiển thị lộ trình tuyến ${formatRouteNumber(journey.route.number)} lên bản đồ!`
                          );
                          handleClose();
                        };

                        return (
                          <TransitResults
                            results={msg.transitResults}
                            onSelectStop={(stop) => {
                              const matchingJourney = allJourneys.find(
                                (j) => j.boardingStop.id === stop.id || j.alightingStop.id === stop.id
                              ) ?? (allJourneys.length > 0 ? allJourneys[0] : null);

                              if (matchingJourney && onRouteReceived) {
                                const parent = (msg.transitResults ?? []).find((r) =>
                                  r.journeys.includes(matchingJourney)
                                );
                                triggerJourneyRoute(matchingJourney, parent?.origin, parent?.destination);
                                return;
                              }

                              if (onSelectPlaceOnMap) {
                                onSelectPlaceOnMap(
                                  stop.latitude,
                                  stop.longitude,
                                  stop.name,
                                  {
                                    id: String(stop.id),
                                    name: stop.name,
                                    address: stop.address,
                                    category: stop.mode === "Metro" ? "Ga metro" : "Trạm xe buýt",
                                  }
                                );
                                toast.success(`Đã hiển thị trạm "${stop.name}" lên bản đồ!`);
                                handleClose();
                              }
                            }}
                            onSelectJourney={
                              onRouteReceived
                                ? (journey, origin, destination) => triggerJourneyRoute(journey, origin, destination)
                                : undefined
                            }
                            onScheduleMeeting={
                              onScheduleMeeting
                                ? (place) => onScheduleMeeting(place, [])
                                : undefined
                            }
                          />
                        );
                      })()}

                      {/* 4. Thẻ tuyến đường được AI tính toán qua get_directions */}
                      {msg.attachedRoute && (
                        <div className="mt-2.5 rounded-xl border border-blue-500/30 bg-blue-500/5 p-2.5 text-xs space-y-1.5">
                          <div className="flex items-center justify-between text-blue-600 font-semibold text-[11px]">
                            <span className="flex items-center gap-1">
                              <Navigation2 className="h-3.5 w-3.5" />
                              Tuyến đường: {msg.attachedRoute.memberName || msg.attachedRoute.originName}
                            </span>
                            <Badge variant="secondary" className="text-[10px] bg-blue-100 text-blue-700">
                              ~{Math.round(msg.attachedRoute.durationMinutes)} phút ({msg.attachedRoute.distanceKm.toFixed(1)} km)
                            </Badge>
                          </div>
                          <div className="text-[11px] text-muted-foreground">
                            Từ <strong className="text-foreground">{msg.attachedRoute.originName}</strong> đến <strong className="text-foreground">{msg.attachedRoute.destinationName}</strong>
                            {msg.attachedRoute.summary && <span> ({msg.attachedRoute.summary})</span>}
                          </div>
                          <div className="pt-1.5 border-t border-blue-500/20">
                            <Button
                              size="sm"
                              variant="outline"
                              onClick={() => {
                                onRouteReceived?.(msg.attachedRoute!);
                                toast.success(`Đã hiển thị tuyến đường lên bản đồ!`);
                                handleClose();
                              }}
                              className="w-full h-7 text-[11px] font-medium gap-1.5 text-blue-600 hover:text-blue-700 border-blue-200 bg-blue-50/60 hover:bg-blue-100/70 dark:text-blue-400 dark:border-blue-800 dark:bg-blue-950/40 cursor-pointer justify-center rounded-lg shadow-2xs"
                            >
                              <Compass className="h-3.5 w-3.5 text-primary" /> Xem tuyến đường trên bản đồ
                            </Button>
                          </div>
                        </div>
                      )}

                      {/* 5. Thẻ danh sách các địa điểm */}
                      {msg.meetingPlaces && msg.meetingPlaces.length > 0 && (
                        <div className="mt-2.5 pt-2 border-t border-border/60 space-y-2">
                          <div className="flex items-center justify-between gap-1.5 flex-wrap">
                            <span className="text-[11px] font-bold text-foreground flex items-center gap-1">
                              <CheckCircle2 className="h-3.5 w-3.5 text-emerald-600" />
                              Địa điểm gợi ý ({msg.meetingPlaces.length}):
                            </span>
                            <div className="flex items-center gap-1.5">
                              <span className="text-[10px] text-muted-foreground flex items-center gap-1 font-medium">
                                <ArrowDownWideNarrow className="h-3 w-3 text-primary" />
                                Xếp theo Điểm tổng hợp
                              </span>
                              {msg.rankingMethod === "onnx" || msg.meetingPlaces.some((p) => p.isOnnxScored) ? (
                                <Badge
                                  variant="outline"
                                  className="text-[9px] px-1.5 py-0 h-4.5 font-medium bg-purple-500/10 text-purple-700 dark:text-purple-300 border-purple-300/40 flex items-center gap-1 shadow-2xs"
                                  title="Điểm số được xếp hạng bằng Mô hình AI LambdaMART (ONNX)"
                                >
                                  <Sparkles className="h-2.5 w-2.5 text-purple-600 dark:text-purple-400" />
                                  Mô hình AI (LambdaMART)
                                </Badge>
                              ) : (
                                <Badge
                                  variant="outline"
                                  className="text-[9px] px-1.5 py-0 h-4.5 font-medium bg-blue-500/10 text-blue-700 dark:text-blue-300 border-blue-300/40 flex items-center gap-1 shadow-2xs"
                                  title="Điểm số được xếp hạng bằng Thuật toán đa tiêu chí CRADIS-DN4 (2023)"
                                >
                                  <Cpu className="h-2.5 w-2.5 text-blue-600 dark:text-blue-400" />
                                  CRADIS-DN4 (2023)
                                </Badge>
                              )}
                            </div>
                          </div>

                          <div className="space-y-2">
                            {[...msg.meetingPlaces].sort((a, b) => (b.finalScore ?? 0) - (a.finalScore ?? 0)).map((item, idx) => {
                              const matchPercent = Math.round((item.relevanceScore ?? 1) * 100);
                              const distancePercent = Math.round((item.distanceScore ?? 1) * 100);
                              const fairnessPercent = Math.round((item.fairnessScore ?? 1) * 100);
                              const finalScoreDisplay = ((item.finalScore ?? 0) * 10).toFixed(1);
                              const isSelected = activeMeetingPlaceId === item.placeId;
                              const isGroup = msg.origins && msg.origins.length > 1;

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
                                    <div className="flex flex-col items-end shrink-0">
                                      <Badge className="text-[9px] font-bold px-1.5 py-0 bg-primary text-primary-foreground shadow-2xs">
                                        ⭐ {finalScoreDisplay}/10
                                      </Badge>
                                      <span className="text-[8px] text-muted-foreground font-medium">
                                        Điểm tổng hợp
                                      </span>
                                    </div>
                                  </div>

                                  {/* Hiển thị cả 3 độ: Độ khớp, Độ tiện đường, Độ công bằng */}
                                  <div className="flex items-center gap-1 flex-wrap pt-0.5">
                                    <span
                                      className="inline-flex items-center gap-0.5 text-[9px] px-1 py-0.5 rounded bg-blue-500/10 text-blue-600 dark:text-blue-400 font-medium border border-blue-500/20"
                                      title="Độ khớp: Mức độ đúng món, thể loại địa điểm theo yêu cầu"
                                    >
                                      <Target className="h-2.5 w-2.5" />
                                      Độ khớp: {matchPercent}%
                                    </span>
                                    <span
                                      className="inline-flex items-center gap-0.5 text-[9px] px-1 py-0.5 rounded bg-amber-500/10 text-amber-600 dark:text-amber-400 font-medium border border-amber-500/20"
                                      title="Độ tiện đường: Quãng đường di chuyển trung bình nhanh và gần"
                                    >
                                      <Car className="h-2.5 w-2.5" />
                                      Tiện đường: {distancePercent}%
                                    </span>
                                    <span
                                      className="inline-flex items-center gap-0.5 text-[9px] px-1 py-0.5 rounded bg-emerald-500/10 text-emerald-600 dark:text-emerald-400 font-medium border border-emerald-500/20"
                                      title="Độ công bằng: Mức độ cân bằng quãng đường/thời gian giữa các người đi"
                                    >
                                      <Scale className="h-2.5 w-2.5" />
                                      Công bằng: {fairnessPercent}%
                                    </span>
                                  </div>

                                  {item.address && (
                                    <p className="text-[10px] text-muted-foreground flex items-center gap-1 truncate">
                                      <MapPin className="h-3 w-3 shrink-0 text-muted-foreground" />
                                      <span className="truncate">{item.address}</span>
                                    </p>
                                  )}

                                  {/* Hiển thị khoảng cách: Nhóm nhiều người vs Cá nhân 1 người */}
                                  {isGroup ? (
                                    (
                                      <div className="bg-muted/40 rounded-lg p-2 space-y-1.5 border border-border/50 text-[10px]">
                                        <div className="font-semibold text-muted-foreground text-[9px] uppercase tracking-wider flex items-center justify-between">
                                          <span className="flex items-center gap-1">
                                            <Users className="h-3 w-3 text-primary" />
                                            Khoảng cách từng người (bấm để xem đường đi):
                                          </span>
                                        </div>
                                        <div
                                          className={`grid gap-1.5 ${
                                            isMaximized
                                              ? "grid-cols-2 sm:grid-cols-3"
                                              : "grid-cols-2 max-h-28 overflow-y-auto pr-1"
                                          }`}
                                        >
                                          {msg.origins!.map((orig, oIdx) => {
                                            const dist =
                                              item.distanceKmByOrigin && item.distanceKmByOrigin[oIdx] !== undefined
                                                ? item.distanceKmByOrigin[oIdx]
                                                : getDistanceKm(orig.lat, orig.lng, item.latitude, item.longitude);
                                            const dur =
                                              item.durationMinutesByOrigin && item.durationMinutesByOrigin[oIdx] !== undefined
                                                ? item.durationMinutesByOrigin[oIdx]
                                                : (dist / 25) * 60;
                                            return (
                                              <button
                                                type="button"
                                                key={oIdx}
                                                onClick={() => {
                                                  if (onSelectOriginRoute) {
                                                    onSelectOriginRoute(item, orig);
                                                  } else {
                                                    onSelectMeetingPlace?.(item, true);
                                                  }
                                                }}
                                                className="flex items-center justify-between gap-1 text-[10px] p-1.5 rounded-md border border-border/40 bg-background/60 hover:bg-muted hover:border-primary/50 text-foreground transition truncate text-left cursor-pointer"
                                                title={`Xem tuyến đường từ ${orig.name} (${orig.address}) đến ${item.name}`}
                                              >
                                                <span className="font-semibold truncate max-w-[65px]">
                                                  {orig.name}:
                                                </span>
                                                <span className="text-muted-foreground shrink-0">
                                                  {dist.toFixed(1)} km
                                                  {dur !== undefined && dur !== null && (
                                                    <span className="ml-0.5 font-bold text-foreground">
                                                      ({Math.round(dur)}p)
                                                    </span>
                                                  )}
                                                </span>
                                              </button>
                                            );
                                          })}
                                        </div>
                                      </div>
                                    )
                                  ) : (
                                    <div className="bg-muted/40 rounded-lg p-2 space-y-1 border border-border/50 text-[10px]">
                                      <div className="flex items-center gap-1.5 text-muted-foreground">
                                        <Car className="h-3 w-3 text-primary" />
                                        <span className="font-semibold text-foreground">Khoảng cách:</span>
                                        <span className="font-bold text-foreground">
                                          {(item.averageDistanceKm ?? 0).toFixed(1)} km
                                        </span>
                                        {item.averageDurationMinutes !== undefined && item.averageDurationMinutes !== null && (
                                          <span className="text-muted-foreground">
                                            (~{Math.round(item.averageDurationMinutes)} phút di chuyển)
                                          </span>
                                        )}
                                      </div>
                                    </div>
                                  )}

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
                                      <Compass className="h-3 w-3" /> Xem đường đi
                                    </Button>
                                    <Button
                                      size="sm"
                                      onClick={(e) => {
                                        e.stopPropagation();
                                        const invitees = selectedFriends.length > 0
                                          ? [
                                              ...(currentUser?.email
                                                ? [{ name: currentUser.username || "Tôi", email: currentUser.email }]
                                                : []),
                                              ...selectedFriends.map((f) => ({ name: f.name, email: f.email })),
                                            ]
                                          : [
                                              ...(currentUser?.email
                                                ? [{ name: currentUser.username || "Tôi", email: currentUser.email }]
                                                : []),
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
                                      className="h-6 text-[10px] px-2 gap-1 bg-black text-white hover:bg-neutral-800 dark:bg-white dark:text-black dark:hover:bg-neutral-200 flex-1 cursor-pointer"
                                    >
                                      <Calendar className="h-3 w-3" /> Đặt điểm hẹn
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

                {/* Hiển thị Live Steps trong lúc AI đang chạy */}
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
                <div ref={chatEndRef} />
              </div>

              {/* Quick Prompts gợi ý */}
              {messages.length <= 2 && !loading && (
                <div className={`flex items-center gap-1.5 overflow-x-auto px-3.5 py-1.5 border-t bg-muted/10 no-scrollbar ${isMaximized ? "max-w-4xl mx-auto w-full" : ""}`}>
                  {QUICK_PROMPTS.map((prompt, idx) => (
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

              {/* Input Footer */}
              <div className="border-t p-2.5 bg-background shrink-0">
                <form
                  onSubmit={(e) => {
                    e.preventDefault();
                    void handleSendMessage();
                  }}
                  autoComplete="off"
                  className={`flex items-end gap-2 ${isMaximized ? "max-w-4xl mx-auto w-full" : ""}`}
                >
                  <textarea
                    ref={textareaRef}
                    name="ai_agent_query_unified_no_autofill"
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
          </Card>
        </div>
      )}
    </>
  );
}
