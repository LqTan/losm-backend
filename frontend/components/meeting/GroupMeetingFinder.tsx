"use client";

import { useEffect, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { api, ApiError } from "@/lib/api";
import { getToken, getUser, StoredUser, AUTH_CHANGE_EVENT } from "@/lib/auth";
import type { MeetingPlace, SearchMeetingPlacesResponse, Place } from "@/types/place";
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
  Send,
  UserCheck,
  Plus,
} from "lucide-react";
import { toast } from "sonner";

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

interface Props {
  userLocation: { lat: number; lng: number } | null;
  isOpen: boolean;
  activeMeetingPlaceId?: string | null;
  onClose: () => void;
  onSearchResults: (
    results: MeetingPlace[],
    centroid: { lat: number; lng: number } | null,
    origins: Array<{ id: string; name: string; address: string; lat: number; lng: number }>
  ) => void;
  onSelectMeetingPlace: (place: MeetingPlace, shouldCloseModal?: boolean) => void;
  onScheduleMeeting: (place: Place, participants?: Array<{ name: string; email: string }>) => void;
}

export default function GroupMeetingFinder({
  userLocation,
  isOpen,
  activeMeetingPlaceId,
  onClose,
  onSearchResults,
  onSelectMeetingPlace,
  onScheduleMeeting,
}: Props) {
  const [friends, setFriends] = useState<FriendWithLocation[]>([]);
  const [loadingFriends, setLoadingFriends] = useState(false);

  const [aiPrompt, setAiPrompt] = useState(
    "Tôi đang ở Chợ Bến Thành và bạn tôi đang ở UBND Quận 1, tìm 1 quán ăn phù hợp"
  );
  const [loading, setLoading] = useState(false);
  const [aiInsight, setAiInsight] = useState<string | null>(null);
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
            selected: true,
          };
        });

        setFriends(realFriends);
      } catch (err: any) {
        if (err instanceof ApiError && err.status === 401) {
          // Token hết hạn hoặc chưa đăng nhập hợp lệ
          setFriends([]);
        } else {
          console.warn("Could not load friends list:", err);
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

  useEffect(() => {
    if (activeMeetingPlaceId) {
      setSelectedResultId(activeMeetingPlaceId);
    }
  }, [activeMeetingPlaceId]);

  if (!isOpen) return null;

  function toggleFriend(id: string) {
    setFriends((prev) =>
      prev.map((f) => (f.id === id ? { ...f, selected: !f.selected } : f))
    );
  }

  const selectedFriends = friends.filter((f) => f.selected);

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

      // Gọi endpoint phân tích AI và tối ưu hóa điểm hẹn nhóm qua MiniMax & OSRM
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
      }>("/api/agent/meeting-search", payload, token);

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

      onSearchResults(items, centroidCoord, returnedOrigins);

      if (items.length > 0) {
        setSelectedResultId(items[0].placeId);
        onSelectMeetingPlace(items[0], false);
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

  return (
    <div
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
      className={`fixed inset-0 z-[1050] flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 overflow-y-auto animate-in fade-in duration-200 ${
        !isOpen ? "hidden pointer-events-none" : ""
      }`}
    >
      <div className="relative w-full max-w-xl bg-card border border-border rounded-2xl shadow-2xl flex flex-col max-h-[90vh] overflow-hidden">
        {/* Header - Phong cách Slate/Dark nhất quán */}
        <div className="flex items-center justify-between px-6 py-4 border-b border-border bg-muted/40">
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

        {/* Modal Body */}
        <div className="overflow-y-auto p-6 space-y-5 text-foreground">
          {/* 1. Thanh nhập yêu cầu cho Trợ lý AI */}
          <div className="space-y-2">
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
              placeholder="Ví dụ: Tôi đang ở Chợ Bến Thành và bạn tôi đang ở UBND Quận 1, tìm 1 quán ăn phù hợp..."
              className="text-xs py-2.5 px-3 rounded-xl border-border bg-background resize-y min-h-[90px] max-h-[240px] focus-visible:ring-primary leading-relaxed"
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

          {/* 3. Nhận xét từ Trợ lý AI (nếu có) */}
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

          {/* 4. Chọn thêm bạn bè trong danh bạ (Tùy chọn - Không bắt buộc) */}
          <div className="space-y-2.5 pt-2 border-t border-border/70">
            <div className="flex items-center justify-between">
              <label className="text-xs font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
                <UserCheck className="h-3.5 w-3.5 text-primary" />
                Chọn thêm bạn bè trong danh bạ (Tùy chọn)
              </label>
              <span className="text-[11px] text-muted-foreground">
                {selectedFriends.length > 0 ? `Đã chọn ${selectedFriends.length} bạn` : "Không bắt buộc"}
              </span>
            </div>

            {/* Vị trí của Bạn */}
            <div className="p-3 rounded-xl border border-primary/20 bg-primary/5 flex items-center justify-between gap-3">
              <div className="flex items-center gap-2.5 min-w-0">
                {currentUser?.avatarUrl ? (
                  <img
                    src={currentUser.avatarUrl}
                    alt={currentUser.username || "Tôi"}
                    className="h-7 w-7 rounded-full object-cover border border-border shrink-0"
                    referrerPolicy="no-referrer"
                  />
                ) : (
                  <div className="flex h-7 w-7 items-center justify-center rounded-full bg-primary text-primary-foreground font-bold text-xs shrink-0">
                    Tôi
                  </div>
                )}
                <div className="min-w-0">
                  <div className="text-xs font-bold text-foreground">
                    {currentUser?.username ? `Vị trí của ${currentUser.username} (Tôi)` : "Vị trí của bạn"}
                  </div>
                  <div className="text-[11px] text-muted-foreground truncate">
                    📍 {userLocation ? "Toạ độ GPS hiện tại" : "Quận 1 (Mặc định)"}
                  </div>
                </div>
              </div>
              <Badge variant="outline" className="text-[10px] bg-background text-primary shrink-0">
                Chủ trì
              </Badge>
            </div>

            {/* Danh sách bạn bè */}
            {loadingFriends ? (
              <div className="flex items-center justify-center py-6 text-xs text-muted-foreground gap-2">
                <Loader2 className="h-4 w-4 animate-spin text-primary" />
                Đang tải danh sách thành viên...
              </div>
            ) : friends.length === 0 ? (
              <div className="p-4 rounded-xl border border-dashed border-border text-center space-y-1.5 bg-muted/20">
                <p className="text-xs font-semibold text-foreground">Chưa có bạn bè trong danh sách</p>
                <p className="text-[11px] text-muted-foreground max-w-sm mx-auto">
                  Bạn có thể nhập trực tiếp vị trí các bạn trong ô yêu cầu AI ở trên hoặc thêm bạn bè qua danh bạ.
                </p>
                <div className="pt-1">
                  <a
                    href="/profile/friends"
                    className="inline-flex items-center gap-1.5 px-3 py-1.5 rounded-lg bg-primary text-primary-foreground hover:bg-primary/90 text-xs font-medium transition shadow-sm"
                  >
                    <Plus className="h-3.5 w-3.5" />
                    <span>Thêm bạn bè mới</span>
                  </a>
                </div>
              </div>
            ) : (
              <div className="space-y-2">
                <div className="flex flex-col gap-2 max-h-44 overflow-y-auto pr-1">
                  {friends.map((friend) => (
                    <div
                      key={friend.id}
                      onClick={() => toggleFriend(friend.id)}
                      className={`p-2.5 rounded-xl border text-left transition flex items-center justify-between gap-3 cursor-pointer ${
                        friend.selected
                          ? "border-primary/50 bg-primary/5 shadow-xs"
                          : "border-border bg-card hover:bg-muted/50"
                      }`}
                    >
                      <div className="flex items-center gap-2.5 min-w-0 flex-1">
                        {friend.avatarUrl ? (
                          <img
                            src={
                              friend.avatarUrl.startsWith("http")
                                ? `/api/avatar?url=${encodeURIComponent(friend.avatarUrl)}`
                                : friend.avatarUrl
                            }
                            alt={friend.name}
                            className="h-8 w-8 rounded-full object-cover border border-border shrink-0"
                            referrerPolicy="no-referrer"
                          />
                        ) : (
                          <div className="flex h-8 w-8 items-center justify-center rounded-full bg-primary/10 text-primary font-bold text-xs shrink-0">
                            {friend.name.charAt(0).toUpperCase()}
                          </div>
                        )}
                        <div className="min-w-0 flex-1">
                          <div className="text-xs font-semibold text-foreground truncate">
                            {friend.name}
                          </div>
                          <div className="text-[11px] text-muted-foreground truncate">
                            {friend.email}
                          </div>
                        </div>
                      </div>
                      <div className="flex items-center gap-1.5 shrink-0">
                        <div
                          className={`flex h-5 w-5 shrink-0 items-center justify-center rounded-md border text-xs font-bold transition shadow-xs ${
                            friend.selected
                              ? "bg-black border-black text-white"
                              : "border-border bg-background text-transparent hover:border-foreground/40"
                          }`}
                        >
                          ✓
                        </div>
                      </div>
                    </div>
                  ))}
                </div>
                <div className="flex justify-end pt-1">
                  <a
                    href="/profile/friends"
                    className="text-[11px] text-primary hover:underline flex items-center gap-1 font-medium"
                  >
                    <span>+ Quản lý & thêm bạn bè</span>
                  </a>
                </div>
              </div>
            )}
          </div>

          {/* Kết quả tìm kiếm */}
          {hasSearched && (
            <div className="space-y-3 pt-2 border-t border-border">
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
                <div className="space-y-2.5 max-h-64 overflow-y-auto pr-1">
                  {results.map((item, idx) => {
                    const isSelected = selectedResultId === item.placeId;
                    const fairnessPercent = Math.round(
                      (item.fairnessScore ?? 0.8) * 100
                    );

                    return (
                      <div
                        key={item.placeId}
                        onClick={() => {
                          setSelectedResultId(item.placeId);
                          onSelectMeetingPlace(item, false);
                        }}
                        className={`p-3 rounded-xl border transition cursor-pointer space-y-2 ${
                          isSelected
                            ? "border-primary bg-primary/5 shadow-sm ring-1 ring-primary/30"
                            : "border-border bg-card hover:bg-muted/40"
                        }`}
                      >
                        <div className="flex items-start justify-between gap-2">
                          <div>
                            <div className="flex items-center gap-2">
                              <span className="flex h-4 w-4 items-center justify-center rounded-full bg-foreground text-background text-[10px] font-bold">
                                {idx + 1}
                              </span>
                              <h4 className="font-bold text-xs text-foreground leading-tight">
                                {item.name}
                              </h4>
                            </div>
                            {item.address && (
                              <p className="text-[11px] text-muted-foreground line-clamp-1 mt-0.5 ml-6">
                                {item.address}
                              </p>
                            )}
                          </div>

                          <Badge
                            variant="secondary"
                            className="shrink-0 text-[10px] font-bold px-2 py-0.5 bg-emerald-50 text-emerald-700 border-emerald-200"
                          >
                            ⚖️ Công bằng: {fairnessPercent}%
                          </Badge>
                        </div>

                        {/* Chi tiết lộ trình OSRM từng người */}
                        <div className="bg-muted/60 rounded-lg p-2 text-xs space-y-1">
                          <div className="text-[10px] font-medium text-muted-foreground flex items-center justify-between pb-1 border-b border-border/50">
                            <span className="flex items-center gap-1">
                              <Car className="h-3 w-3 text-primary" />
                              {item.isRoadRouting
                                ? "Lộ trình thực tế (OSRM)"
                                : "Khoảng cách đường thẳng"}
                            </span>
                            {item.spreadMinutes !== undefined && item.spreadMinutes !== null && (
                              <span>Lệch: ~{item.spreadMinutes} phút</span>
                            )}
                          </div>

                          <div className="grid grid-cols-2 gap-x-2 gap-y-1 text-[11px] pt-0.5">
                            {activeOrigins.length > 0 ? (
                              activeOrigins.map((orig, i) => {
                                const dist = item.distanceKmByOrigin?.[i] ?? 0;
                                const dur = item.durationMinutesByOrigin?.[i];

                                return (
                                  <div
                                    key={orig.id || `orig_${i}`}
                                    className="flex items-center gap-1 text-muted-foreground truncate"
                                  >
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
                              onSelectMeetingPlace(item, true);
                              onClose();
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
                              const invitees = activeOrigins.length > 0
                                ? activeOrigins.map((o) => ({ name: o.name, email: o.address || "" }))
                                : [
                                    ...(currentUser?.email
                                      ? [{ name: currentUser.username || "Tôi", email: currentUser.email }]
                                      : []),
                                    ...selectedFriends.map((f) => ({ name: f.name, email: f.email })),
                                  ];
                              onScheduleMeeting(
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
      </div>
    </div>
  );
}
