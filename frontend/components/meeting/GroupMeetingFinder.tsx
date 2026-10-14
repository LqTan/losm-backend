"use client";

import { useEffect, useState, useRef } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { api, ApiError } from "@/lib/api";
import { getToken, getUser, StoredUser, AUTH_CHANGE_EVENT } from "@/lib/auth";
import type { MeetingPlace, Place } from "@/types/place";
import {
  Users,
  MapPin,
  Sparkles,
  Car,
  CheckCircle2,
  Calendar,
  X,
  Loader2,
  Bot,
  UserCheck,
  Plus,
  Search,
} from "lucide-react";
import { toast } from "sonner";

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

export interface OriginItem {
  id: string;
  name: string;
  address: string;
  lat: number;
  lng: number;
  avatarUrl?: string;
}

export interface FriendWithLocation {
  id: string;
  name: string;
  email: string;
  avatarUrl?: string;
  addressName: string;
  lat: number;
  lng: number;
  selected?: boolean;
}

export const COMMON_AREAS = [
  { name: "Quận 1 (Trung tâm)", lat: 10.7769, lng: 106.7009 },
  { name: "Quận 3 (Hồ Con Rùa)", lat: 10.7828, lng: 106.6958 },
  { name: "Bình Thạnh (Hàng Xanh)", lat: 10.8016, lng: 106.7114 },
  { name: "Phú Nhuận (Phan Xích Long)", lat: 10.7977, lng: 106.6872 },
  { name: "Quận 10 (Vạn Hạnh Mall)", lat: 10.7711, lng: 106.6698 },
  { name: "Tân Bình (Lê Văn Sỹ)", lat: 10.7941, lng: 106.6612 },
  { name: "Quận 5 (Chợ Lớn)", lat: 10.7544, lng: 106.6633 },
  { name: "Quận 7 (Phú Mỹ Hưng)", lat: 10.7292, lng: 106.7126 },
  { name: "Thủ Đức (Thảo Điền)", lat: 10.8038, lng: 106.7369 },
  { name: "Thủ Đức (Lê Văn Việt)", lat: 10.8505, lng: 106.7725 },
  { name: "Gò Vấp (Quang Trung)", lat: 10.8384, lng: 106.6651 },
  { name: "Tân Phú (Aeon Mall)", lat: 10.8009, lng: 106.6180 },
];

export interface GroupMeetingContentProps {
  userLocation: { lat: number; lng: number } | null;
  activeMeetingPlaceId?: string | null;
  onSearchResults?: (
    results: MeetingPlace[],
    centroid: { lat: number; lng: number } | null,
    origins: Array<{ id: string; name: string; address: string; lat: number; lng: number }>
  ) => void;
  onSelectMeetingPlace?: (place: MeetingPlace, shouldCloseModal?: boolean) => void;
  onScheduleMeeting?: (place: Place, participants?: Array<{ name: string; email: string }>) => void;
  onClose?: () => void;
  isMaximized?: boolean;
  resetSignal?: number;
}

export function GroupMeetingContent({
  userLocation,
  activeMeetingPlaceId,
  onSearchResults,
  onSelectMeetingPlace,
  onScheduleMeeting,
  onClose,
  isMaximized = false,
  resetSignal = 0,
}: GroupMeetingContentProps) {
  const [friends, setFriends] = useState<FriendWithLocation[]>([]);
  const [loadingFriends, setLoadingFriends] = useState(false);
  const [friendSearchQuery, setFriendSearchQuery] = useState("");
  const [showFriendDropdown, setShowFriendDropdown] = useState(false);
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

  const [aiPrompt, setAiPrompt] = useState(
    "Tôi đang ở Chợ Bến Thành và bạn tôi đang ở UBND Quận 1, tìm 1 quán ăn phù hợp"
  );
  const [loading, setLoading] = useState(false);
  const [aiInsight, setAiInsight] = useState<string | null>(null);
  const [supportedAreas, setSupportedAreas] = useState<string[]>([]);
  const [results, setResults] = useState<MeetingPlace[]>([]);
  const [activeOrigins, setActiveOrigins] = useState<OriginItem[]>([]);
  const [hasSearched, setHasSearched] = useState(false);
  const [selectedResultId, setSelectedResultId] = useState<string | null>(null);
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
        } else {
          console.warn("Could not load friends list:", err);
        }
      } finally {
        setLoadingFriends(false);
      }
    }

    setCurrentUser(getUser());
    fetchFriends();
  }, []);

  useEffect(() => {
    if (activeMeetingPlaceId) {
      setSelectedResultId(activeMeetingPlaceId);
    }
  }, [activeMeetingPlaceId]);

  // Lắng nghe resetSignal để xóa kết quả tìm kiếm khi bấm Tạo chat mới
  const isFirstRender = useRef(true);
  useEffect(() => {
    if (isFirstRender.current) {
      isFirstRender.current = false;
      return;
    }
    setResults([]);
    setActiveOrigins([]);
    setHasSearched(false);
    setAiInsight(null);
    setSelectedResultId(null);
    setFriendSearchQuery("");
    setShowFriendDropdown(false);
    setFriends((prev) => prev.map((f) => ({ ...f, selected: false })));
  }, [resetSignal]);

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

  async function handleFindPlaces() {
    const promptText = aiPrompt.trim();
    if (!promptText) {
      toast.error("Vui lòng nhập yêu cầu địa điểm cho Trợ lý AI");
      return;
    }

    const myLat = userLocation?.lat ?? 10.7769;
    const myLng = userLocation?.lng ?? 106.7009;

    setLoading(true);
    setAiInsight(null);

    try {
      const token = getToken();

      const payload = {
        prompt: promptText,
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

      if (res?.supportedAreas) {
        setSupportedAreas(res.supportedAreas);
      }

      const items = res?.results ?? [];
      const returnedOrigins: OriginItem[] =
        res?.origins && res.origins.length > 0
          ? res.origins
          : [
              {
                id: "me",
                name: currentUser?.username ? `${currentUser.username} (Tôi)` : "Tôi",
                address: userLocation ? "GPS Hiện tại" : "Quận 1 (Trung tâm)",
                lat: myLat,
                lng: myLng,
                avatarUrl: currentUser?.avatarUrl,
              },
              ...selectedFriends.map((f) => ({
                id: f.id,
                name: f.name,
                address: f.addressName,
                lat: f.lat,
                lng: f.lng,
                avatarUrl: f.avatarUrl,
              })),
            ];

      setResults(items);
      setActiveOrigins(returnedOrigins);
      setHasSearched(true);

      if (res?.aiInsight) {
        setAiInsight(res.aiInsight);
      }

      const centroidCoord = res?.centroid ? { lat: res.centroid.lat, lng: res.centroid.lng } : null;

      onSearchResults?.(items, centroidCoord, returnedOrigins);

      if (items.length > 0) {
        setSelectedResultId(items[0].placeId);
        onSelectMeetingPlace?.(items[0], false);
        toast.success(`Đã tìm thấy ${items.length} điểm hẹn tối ưu nhất cho nhóm!`);
      } else {
        toast.info("Không tìm thấy địa điểm phù hợp. Hãy thử thay đổi yêu cầu.");
      }
    } catch (err) {
      console.error("Group meeting search error:", err);
      const msg =
        err instanceof ApiError
          ? err.message
          : "Có lỗi khi phân tích và tìm kiếm điểm hẹn. Vui lòng thử lại.";
      toast.error(msg);
    } finally {
      setLoading(false);
    }
  }

  // Left/Form Column Elements
  const formSection = (
    <div className="space-y-4">
      {/* 1. Nhập yêu cầu cho Trợ lý AI */}
      <div className="space-y-1.5">
        <label className="text-xs font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
          <Sparkles className="h-3.5 w-3.5 text-primary" />
          Yêu cầu cho Trợ lý AI
        </label>
        <Textarea
          rows={3}
          value={aiPrompt}
          onChange={(e) => setAiPrompt(e.target.value)}
          onKeyDown={(e) => {
            if (e.key === "Enter" && !e.shiftKey && !loading) {
              e.preventDefault();
              handleFindPlaces();
            }
          }}
          placeholder="Ví dụ: Nhóm mình có 3 người, muốn tìm công viên để gặp bạn bè và đi dạo..."
          className="text-xs py-2.5 px-3 rounded-xl border-border bg-background resize-y min-h-[85px] max-h-[220px] focus-visible:ring-primary leading-relaxed"
        />
      </div>

      {/* 2. Nút Tìm kiếm chính */}
      <Button
        type="button"
        onClick={handleFindPlaces}
        disabled={loading}
        className="w-full h-11 rounded-xl bg-primary text-primary-foreground hover:bg-primary/90 font-semibold text-xs shadow-md"
      >
        {loading ? (
          <>
            <Loader2 className="h-4 w-4 mr-2 animate-spin" />
            Đang phân tích địa điểm & tính toán lộ trình OSRM...
          </>
        ) : (
          <>
            <Bot className="h-4 w-4 mr-2" />
            Tìm điểm hẹn công bằng cho nhóm
          </>
        )}
      </Button>

      {/* 3. Thêm bạn bè trong danh bạ */}
      <div className="space-y-2 pt-2 border-t border-border/70">
        <div className="flex items-center justify-between">
          <label className="text-xs font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
            <UserCheck className="h-3.5 w-3.5 text-primary" />
            Thêm bạn bè trong danh bạ (Tùy chọn)
          </label>
          <div className="flex items-center gap-2">
            {selectedFriends.length > 0 && (
              <span className="text-[11px] font-semibold text-primary">
                Đã chọn {selectedFriends.length} bạn
              </span>
            )}
            <a
              href="/profile/friends"
              className="text-[11px] text-muted-foreground hover:text-primary transition"
              title="Quản lý bạn bè"
            >
              + Quản lý bạn bè
            </a>
          </div>
        </div>

        {/* Thanh tìm kiếm bạn bè với Dropup */}
        <div className="relative" ref={dropdownContainerRef}>
          <div className="relative flex items-center">
            <Search className="absolute left-3 h-3.5 w-3.5 text-muted-foreground pointer-events-none" />
            <Input
              type="text"
              name="friend_search_query_no_autofill"
              autoComplete="off"
              autoCorrect="off"
              autoCapitalize="off"
              spellCheck={false}
              value={friendSearchQuery}
              onChange={(e) => {
                setFriendSearchQuery(e.target.value);
                setShowFriendDropdown(true);
              }}
              onFocus={() => {
                setShowFriendDropdown(true);
              }}
              placeholder="Gõ tên hoặc email bạn bè để thêm vào nhóm..."
              className="h-9 pl-8 pr-8 text-xs rounded-xl bg-background border-border focus-visible:ring-1 focus-visible:ring-primary"
            />
            {friendSearchQuery && (
              <button
                type="button"
                onClick={() => {
                  setFriendSearchQuery("");
                  setShowFriendDropdown(false);
                }}
                className="absolute right-2.5 text-muted-foreground hover:text-foreground p-0.5 rounded-full"
                title="Xóa tìm kiếm"
              >
                <X className="h-3.5 w-3.5" />
              </button>
            )}
          </div>

          {showFriendDropdown && (
            <div className="absolute left-0 right-0 bottom-full mb-1.5 z-50 max-h-52 overflow-y-auto rounded-xl border border-border bg-popover text-popover-foreground shadow-2xl p-1.5 space-y-1">
              {loadingFriends ? (
                <div className="flex items-center justify-center py-3 text-xs text-muted-foreground gap-2">
                  <Loader2 className="h-3.5 w-3.5 animate-spin text-primary" />
                  Đang tải danh bạ...
                </div>
              ) : friends.length === 0 ? (
                <div className="py-3 px-3 text-center text-xs text-muted-foreground space-y-1">
                  <p>Danh bạ bạn bè của bạn hiện đang trống.</p>
                  <a
                    href="/profile/friends"
                    className="text-primary hover:underline text-[11px] font-medium inline-block"
                  >
                    + Đến trang Quản lý bạn bè để kết bạn
                  </a>
                </div>
              ) : filteredFriends.length === 0 ? (
                <div className="py-2.5 px-3 text-center text-[11px] text-muted-foreground">
                  Không tìm thấy bạn bè nào khớp với từ khoá &quot;{friendSearchQuery}&quot;.
                </div>
              ) : (
                filteredFriends.map((friend) => (
                  <div
                    key={friend.id}
                    onClick={() => addFriend(friend.id)}
                    className="flex items-center justify-between p-2 rounded-lg hover:bg-muted/70 cursor-pointer transition text-left"
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
                          className="h-6 w-6 rounded-full object-cover shrink-0"
                          referrerPolicy="no-referrer"
                        />
                      ) : (
                        <div className="flex h-6 w-6 items-center justify-center rounded-full bg-primary/10 text-primary font-bold text-[10px] shrink-0">
                          {friend.name.charAt(0).toUpperCase()}
                        </div>
                      )}
                      <div className="min-w-0">
                        <div className="text-xs font-semibold text-foreground truncate">
                          {friend.name}
                        </div>
                        <div className="text-[10px] text-muted-foreground truncate">
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

        {/* Các chip bạn bè đã chọn */}
        {selectedFriends.length > 0 && (
          <div className="flex flex-wrap gap-1.5 pt-1">
            {selectedFriends.map((f) => (
              <span
                key={f.id}
                className="inline-flex items-center gap-1.5 pl-1.5 pr-2 py-1 rounded-full bg-primary/10 text-primary text-xs font-medium border border-primary/20 shadow-2xs"
              >
                {f.avatarUrl ? (
                  <img
                    src={
                      f.avatarUrl.startsWith("http")
                        ? `/api/avatar?url=${encodeURIComponent(f.avatarUrl)}`
                        : f.avatarUrl
                    }
                    alt={f.name}
                    className="h-4 w-4 rounded-full object-cover shrink-0"
                    referrerPolicy="no-referrer"
                  />
                ) : (
                  <span className="flex h-4 w-4 items-center justify-center rounded-full bg-primary text-primary-foreground text-[9px] font-bold shrink-0">
                    {f.name.charAt(0).toUpperCase()}
                  </span>
                )}
                <span className="truncate max-w-[120px]">{f.name}</span>
                <button
                  type="button"
                  onClick={() => removeFriend(f.id)}
                  className="hover:text-destructive transition ml-0.5"
                  title="Xóa bạn này"
                >
                  <X className="h-3 w-3" />
                </button>
              </span>
            ))}
          </div>
        )}
      </div>
    </div>
  );

  // Right/Results Section
  const resultsSection = (
    <div className="space-y-4">
      {/* Nhận xét AI */}
      {aiInsight && (
        <div className="p-3.5 rounded-xl border border-primary/20 bg-primary/5 text-xs text-foreground space-y-1.5">
          <div className="font-bold flex items-center gap-1.5 text-primary">
            <Bot className="h-4 w-4" />
            Trợ lý AI nhận xét:
          </div>
          <p className="text-muted-foreground leading-relaxed whitespace-pre-line text-[11px]">
            {aiInsight}
          </p>
        </div>
      )}

      {/* Kết quả tìm kiếm */}
      {hasSearched && (
        <div className="space-y-3 pt-1 border-t border-border">
          <div className="flex items-center justify-between">
            <h3 className="font-bold text-xs text-foreground flex items-center gap-1.5">
              <CheckCircle2 className="h-4 w-4 text-emerald-600" />
              Địa điểm gợi ý ({results.length})
            </h3>
            <span className="text-[11px] text-muted-foreground">
              Xếp hạng theo độ công bằng
            </span>
          </div>

          {results.length === 0 ? (
            <p className="text-xs text-muted-foreground text-center py-4">
              Không tìm thấy địa điểm nào phù hợp. Hãy thử thay đổi yêu cầu.
            </p>
          ) : (
            <div className={`space-y-2.5 overflow-y-auto pr-1 ${isMaximized ? "max-h-[calc(100vh-280px)]" : "max-h-72"}`}>
              {results.map((item, idx) => {
                const isSelected = selectedResultId === item.placeId;
                const fairnessPercent = Math.round((item.fairnessScore ?? 0.8) * 100);

                return (
                  <div
                    key={item.placeId}
                    onClick={() => {
                      setSelectedResultId(item.placeId);
                      onSelectMeetingPlace?.(item, false);
                    }}
                    className={`p-3 rounded-xl border transition cursor-pointer space-y-2 ${
                      isSelected
                        ? "border-primary bg-primary/5 shadow-sm ring-1 ring-primary/30"
                        : "border-border bg-card hover:bg-muted/40"
                    }`}
                  >
                    <div className="flex items-start justify-between gap-2">
                      <div className="flex items-center gap-2 min-w-0">
                        <span className="flex h-5 w-5 shrink-0 items-center justify-center rounded-full bg-primary/10 text-primary font-bold text-[10px]">
                          {idx + 1}
                        </span>
                        <div className="min-w-0">
                          <h4 className="font-bold text-xs text-foreground truncate">
                            {item.name}
                          </h4>
                          {item.category && (
                            <span className="text-[10px] text-muted-foreground">
                              {item.category}
                            </span>
                          )}
                        </div>
                      </div>
                      <Badge variant="secondary" className="text-[10px] shrink-0 font-medium bg-emerald-500/10 text-emerald-600">
                        Công bằng {fairnessPercent}%
                      </Badge>
                    </div>

                    {item.address && (
                      <p className="text-[11px] text-muted-foreground flex items-center gap-1 truncate">
                        <MapPin className="h-3 w-3 shrink-0 text-muted-foreground" />
                        <span className="truncate">{item.address}</span>
                      </p>
                    )}

                    {/* Chi tiết thời gian di chuyển từng người */}
                    <div className="bg-muted/40 rounded-lg p-2 space-y-1.5 border border-border/50 text-[11px]">
                      <div className="font-semibold text-muted-foreground text-[10px] uppercase tracking-wider flex items-center gap-1">
                        <Car className="h-3 w-3 text-primary" />
                        Khoảng cách & thời gian di chuyển từng người:
                      </div>
                      <div className="grid grid-cols-2 gap-x-2 gap-y-1">
                        {activeOrigins.length > 0 ? (
                          activeOrigins.map((orig, oIdx) => {
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

                    {/* Nút hành động */}
                    <div className="flex items-center gap-2 pt-0.5">
                      <Button
                        type="button"
                        size="sm"
                        variant="outline"
                        onClick={(e) => {
                          e.stopPropagation();
                          setSelectedResultId(item.placeId);
                          onSelectMeetingPlace?.(item, true);
                          onClose?.();
                        }}
                        className="h-7 text-xs flex-1 rounded-lg"
                      >
                        Xem trên bản đồ
                      </Button>
                      <Button
                        type="button"
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
                        className="h-7 text-xs flex-1 rounded-lg bg-primary text-primary-foreground hover:bg-primary/90 font-medium"
                      >
                        <Calendar className="h-3 w-3 mr-1" />
                        Hẹn tại đây
                      </Button>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      )}
    </div>
  );

  return (
    <div className="flex-1 overflow-y-auto p-4 sm:p-5 text-foreground">
      {isMaximized ? (
        <div className="max-w-5xl mx-auto grid grid-cols-1 lg:grid-cols-2 gap-8 py-2">
          {formSection}
          <div>
            {hasSearched || aiInsight ? (
              resultsSection
            ) : (
              <div className="h-full flex flex-col items-center justify-center p-8 rounded-2xl border border-dashed border-border/80 text-center text-muted-foreground space-y-3">
                <div className="h-12 w-12 rounded-2xl bg-primary/10 text-primary flex items-center justify-center">
                  <Users className="h-6 w-6" />
                </div>
                <div>
                  <h4 className="text-sm font-bold text-foreground">Chưa có kết quả điểm hẹn</h4>
                  <p className="text-xs text-muted-foreground mt-1 max-w-sm">
                    Nhập yêu cầu xuất phát của nhóm ở bên trái và bấm &quot;Tìm điểm hẹn công bằng cho nhóm&quot; để AI đề xuất.
                  </p>
                </div>
              </div>
            )}
          </div>
        </div>
      ) : (
        <div className="space-y-5">
          {formSection}
          {(hasSearched || aiInsight) && resultsSection}
        </div>
      )}
    </div>
  );
}

interface ModalProps extends GroupMeetingContentProps {
  isOpen: boolean;
  onClose: () => void;
}

export default function GroupMeetingFinder({
  isOpen,
  onClose,
  ...props
}: ModalProps) {
  if (!isOpen) return null;

  return (
    <div
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
      className="fixed inset-0 z-[1050] flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 overflow-y-auto animate-in fade-in duration-200"
    >
      <div className="relative w-full max-w-xl bg-card border border-border rounded-2xl shadow-2xl flex flex-col max-h-[90vh] overflow-hidden">
        {/* Header */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-border bg-muted/40 shrink-0">
          <div className="flex items-center gap-3">
            <div className="flex h-10 w-10 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <Users className="h-5 w-5" />
            </div>
            <div>
              <h2 className="text-base font-bold text-foreground leading-tight">
                Tìm điểm hẹn chung
              </h2>
              <p className="text-xs text-muted-foreground mt-0.5 flex items-center gap-1.5">
                <Car className="h-3.5 w-3.5 text-primary" />
                Tính toán ma trận lộ trình & thời gian OSRM thực tế
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="flex h-8 w-8 items-center justify-center rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted transition"
            title="Đóng"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        <GroupMeetingContent {...props} onClose={onClose} />
      </div>
    </div>
  );
}
