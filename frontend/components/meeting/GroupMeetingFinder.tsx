"use client";

import { useEffect, useState } from "react";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { api, ApiError } from "@/lib/api";
import { getToken, getUser, StoredUser } from "@/lib/auth";
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
  addressName: string;
  lat: number;
  lng: number;
  selected?: boolean;
}

export const TEST_USERS: FriendWithLocation[] = [
  {
    id: "user_nam",
    name: "Hoàng Nam",
    email: "nam.hoang@losm.vn",
    addressName: "Bình Thạnh (Hàng Xanh)",
    lat: 10.8016,
    lng: 106.7114,
    selected: true,
  },
  {
    id: "user_lan",
    name: "Phương Lan",
    email: "lan.phuong@losm.vn",
    addressName: "Quận 3 (Hồ Con Rùa)",
    lat: 10.7828,
    lng: 106.6958,
    selected: true,
  },
  {
    id: "user_tuan",
    name: "Minh Tuấn",
    email: "tuan.minh@losm.vn",
    addressName: "Phú Nhuận (Phan Xích Long)",
    lat: 10.7977,
    lng: 106.6872,
    selected: false,
  },
  {
    id: "user_vy",
    name: "Khánh Vy",
    email: "vy.khanh@losm.vn",
    addressName: "Quận 10 (Sư Vạn Hạnh)",
    lat: 10.7711,
    lng: 106.6698,
    selected: false,
  },
  {
    id: "user_bao",
    name: "Quốc Bảo",
    email: "bao.quoc@losm.vn",
    addressName: "Tân Bình (Lê Văn Sỹ)",
    lat: 10.7941,
    lng: 106.6612,
    selected: false,
  },
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
  onScheduleMeeting: (place: Place) => void;
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
  const [friends, setFriends] = useState<FriendWithLocation[]>(TEST_USERS);
  const [aiPrompt, setAiPrompt] = useState(
    "Tìm quán cà phê yên tĩnh có không gian họp nhóm và đồ uống ngon"
  );
  const [loading, setLoading] = useState(false);
  const [aiInsight, setAiInsight] = useState<string | null>(null);
  const [results, setResults] = useState<MeetingPlace[]>([]);
  const [hasSearched, setHasSearched] = useState(false);
  const [selectedResultId, setSelectedResultId] = useState<string | null>(null);
  const [currentUser, setCurrentUser] = useState<StoredUser | null>(() => {
    if (typeof window !== "undefined") {
      return getUser();
    }
    return null;
  });

  useEffect(() => {
    if (isOpen) {
      setCurrentUser(getUser());
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

    if (selectedFriends.length === 0) {
      toast.error("Vui lòng chọn ít nhất 1 người bạn để tính điểm hẹn");
      return;
    }

    // Origins = [Tôi, ...selectedFriends]
    const myLat = userLocation?.lat ?? 10.7769;
    const myLng = userLocation?.lng ?? 106.7009;

    const allOrigins: OriginItem[] = [
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
      })),
    ];

    setLoading(true);
    setAiInsight(null);

    try {
      const token = getToken();

      // 1. Tính toán điểm hẹn và định tuyến OSRM
      const payload = {
        query: promptText,
        origins: allOrigins.map((o) => ({
          name: o.name,
          latitude: o.lat,
          longitude: o.lng,
        })),
        radiusKm: 6,
        topK: 10,
      };

      const res = await api.post<SearchMeetingPlacesResponse>(
        "/api/search/meeting-places",
        payload,
        token
      );

      const items = res?.results ?? [];
      setResults(items);
      setHasSearched(true);

      const centroid = res
        ? { lat: res.centroidLatitude, lng: res.centroidLongitude }
        : null;

      onSearchResults(items, centroid, allOrigins);

      if (items.length > 0) {
        setSelectedResultId(items[0].placeId);
        onSelectMeetingPlace(items[0], false);

        // 2. Gọi MiniMax AI để nhận xét & tư vấn ngắn gọn
        try {
          const topPlace = items[0];
          const membersList = ["Tôi", ...selectedFriends.map((f) => f.name)].join(", ");
          const distancesInfo = allOrigins
            .map((o, idx) => {
              const dist = topPlace.distanceKmByOrigin?.[idx];
              const dur = topPlace.durationMinutesByOrigin?.[idx];
              const distStr = dist != null ? `${dist.toFixed(1)} km` : "";
              const durStr = dur != null ? ` (~${Math.round(dur)} phút)` : "";
              return `${o.name}: ${distStr}${durStr}`;
            })
            .join("; ");

          const prompt = `Bạn là trợ lý điểm hẹn nhóm. Nhóm gồm: ${membersList}. Yêu cầu: "${promptText}".
Hệ thống OSRM đã chọn quán tối ưu nhất cho nhóm là: "${topPlace.name}" (${topPlace.address ?? "TP.HCM"}).
Khoảng cách di chuyển: ${distancesInfo}.
Độ công bằng lộ trình: ${Math.round((topPlace.fairnessScore ?? 0.8) * 100)}%.

YÊU CẦU ĐẶC BIỆT: KHÔNG gọi tool tra cứu review hay tìm kiếm lại dữ liệu vì hệ thống hiện tại chưa có dữ liệu review. Hãy trả lời trực tiếp trong 2 đến 3 câu ngắn gọn nhận xét về vị trí địa lý, độ thuận tiện di chuyển công bằng cho nhóm bạn và một lời chúc buổi gặp mặt.`;

          const aiRes = await api.post<{ answer?: string }>(
            "/api/agent",
            {
              message: prompt,
              latitude: centroid?.lat ?? myLat,
              longitude: centroid?.lng ?? myLng,
            },
            token
          );
          if (aiRes?.answer) {
            setAiInsight(aiRes.answer);
          }
        } catch {
          // Bỏ qua nếu MiniMax agent bận, kết quả OSRM vẫn hiển thị trọn vẹn
        }

        toast.success(`Đã tìm thấy ${items.length} điểm hẹn tối ưu nhất cho nhóm!`);
      } else {
        toast.info("Không tìm thấy địa điểm phù hợp. Hãy thử thay đổi yêu cầu.");
      }
    } catch (err) {
      const msg =
        err instanceof ApiError
          ? err.message
          : "Có lỗi khi tính toán điểm hẹn. Vui lòng thử lại.";
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
          {/* Chọn thành viên tham gia */}
          <div className="space-y-2.5">
            <div className="flex items-center justify-between">
              <label className="text-xs font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
                <UserCheck className="h-3.5 w-3.5 text-primary" />
                Chọn thành viên tham gia ({selectedFriends.length + 1} người)
              </label>
              <span className="text-[11px] text-muted-foreground">
                Đã lưu tọa độ GPS lần cuối
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

            {/* Danh sách bạn bè dạng list dọc trên xuống có overflow-y-auto */}
            <div className="flex flex-col gap-2 max-h-48 overflow-y-auto pr-1">
              {friends.map((friend) => (
                <button
                  key={friend.id}
                  type="button"
                  onClick={() => toggleFriend(friend.id)}
                  className={`p-2.5 rounded-xl border text-left transition flex items-center justify-between gap-3 ${
                    friend.selected
                      ? "border-primary bg-primary/10 shadow-sm"
                      : "border-border bg-card hover:bg-muted/50"
                  }`}
                >
                  <div className="flex items-center gap-2.5 min-w-0">
                    <div className="flex h-7 w-7 items-center justify-center rounded-full bg-muted text-muted-foreground font-semibold text-xs shrink-0">
                      {friend.name.charAt(0)}
                    </div>
                    <div className="min-w-0">
                      <div className="text-xs font-semibold text-foreground flex items-center gap-1.5 truncate">
                        <span>{friend.name}</span>
                        <span className="text-[10px] text-muted-foreground font-normal truncate">
                          ({friend.email})
                        </span>
                      </div>
                      <div className="text-[11px] text-muted-foreground truncate">
                        📍 {friend.addressName}
                      </div>
                    </div>
                  </div>
                  <div
                    className={`flex h-5 w-5 shrink-0 items-center justify-center rounded-full border text-[10px] transition ${
                      friend.selected
                        ? "bg-primary border-primary text-primary-foreground font-bold"
                        : "border-border text-transparent"
                    }`}
                  >
                    ✓
                  </div>
                </button>
              ))}
            </div>
          </div>

          {/* Thanh nhập yêu cầu cho Trợ lý AI */}
          <div className="space-y-2">
            <label className="text-xs font-bold uppercase tracking-wider text-muted-foreground flex items-center gap-1.5">
              <Sparkles className="h-3.5 w-3.5 text-primary" />
              Yêu cầu cho Trợ lý AI (MiniMax)
            </label>
            <div className="relative">
              <Input
                value={aiPrompt}
                onChange={(e) => setAiPrompt(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter" && !loading) {
                    handleFindPlaces();
                  }
                }}
                placeholder="Ví dụ: Tìm quán cà phê yên tĩnh có ổ điện để làm việc nhóm..."
                className="pr-12 text-xs py-2.5 rounded-xl border-border bg-background"
              />
              <button
                type="button"
                onClick={handleFindPlaces}
                disabled={loading}
                className="absolute right-2 top-1/2 -translate-y-1/2 flex h-8 w-8 items-center justify-center rounded-lg bg-primary text-primary-foreground hover:bg-primary/90 disabled:opacity-50 transition"
                title="Gửi yêu cầu"
              >
                {loading ? (
                  <Loader2 className="h-4 w-4 animate-spin" />
                ) : (
                  <Send className="h-3.5 w-3.5" />
                )}
              </button>
            </div>
          </div>

          {/* Nút Tìm kiếm chính */}
          <Button
            type="button"
            onClick={handleFindPlaces}
            disabled={loading}
            className="w-full h-11 rounded-xl bg-primary text-primary-foreground hover:bg-primary/90 font-semibold text-xs shadow-md"
          >
            {loading ? (
              <>
                <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                Đang xử lý yêu cầu qua MiniMax & OSRM Table...
              </>
            ) : (
              <>
                <Bot className="h-4 w-4 mr-2" />
                Tìm Điểm Hẹn Công Bằng Cho Nhóm
              </>
            )}
          </Button>

          {/* Nhận xét từ Trợ lý AI (nếu có) */}
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
                            {/* Tôi */}
                            <div className="flex items-center gap-1 text-muted-foreground truncate">
                              <span className="font-semibold text-foreground">Tôi:</span>
                              <span>
                                {(item.distanceKmByOrigin?.[0] ?? 0).toFixed(1)} km
                                {item.durationMinutesByOrigin?.[0] !== undefined &&
                                  item.durationMinutesByOrigin?.[0] !== null && (
                                    <span className="ml-0.5 font-bold text-foreground">
                                      ({Math.round(item.durationMinutesByOrigin[0])}p)
                                    </span>
                                  )}
                              </span>
                            </div>

                            {/* Các bạn bè */}
                            {selectedFriends.map((f, i) => {
                              const dist = item.distanceKmByOrigin?.[i + 1] ?? 0;
                              const dur = item.durationMinutesByOrigin?.[i + 1];

                              return (
                                <div
                                  key={f.id}
                                  className="flex items-center gap-1 text-muted-foreground truncate"
                                >
                                  <span className="font-semibold text-foreground truncate max-w-[50px]">
                                    {f.name}:
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
                            })}
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
                              onScheduleMeeting({
                                id: item.placeId,
                                name: item.name,
                                address: item.address,
                                latitude: item.latitude,
                                longitude: item.longitude,
                                category: item.category,
                              });
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
