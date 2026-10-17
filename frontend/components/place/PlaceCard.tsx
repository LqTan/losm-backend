"use client";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import type { Place, MeetingPlace } from "@/types/place";
import type { OriginItem } from "@/components/meeting/GroupMeetingFinder";
import {
  Calendar,
  ChevronDown,
  ChevronUp,
  Clock,
  Compass,
  ExternalLink,
  Loader2,
  MapPin,
  Navigation2,
  Share2,
  Users,
  X,
} from "lucide-react";
import { useEffect, useState } from "react";
import { toast } from "sonner";

interface Props {
  place: Place;
  activeMeetingPlace?: MeetingPlace | null;
  groupOrigins?: OriginItem[];
  navigationRoute?: {
    distanceKm: number;
    durationMinutes: number;
  } | null;
  loadingDirections?: boolean;
  selectedRouteOriginId?: string | null;
  onClose: () => void;
  onScheduleMeeting?: (place: Place) => void;
  onGetDirections?: (place: Place, origin?: OriginItem) => void;
  onClearDirections?: () => void;
}

export default function PlaceCard({
  place,
  activeMeetingPlace,
  groupOrigins,
  navigationRoute,
  loadingDirections,
  selectedRouteOriginId,
  onClose,
  onScheduleMeeting,
  onGetDirections,
  onClearDirections,
}: Props) {
  const [isCollapsed, setIsCollapsed] = useState(false);

  // Khi chọn một địa điểm mới, tự động mở rộng chi tiết
  useEffect(() => {
    setIsCollapsed(false);
  }, [place.id]);

  const selectedOrigin = groupOrigins?.find((o) => o.id === selectedRouteOriginId);
  const selectedOriginName = selectedOrigin?.name;
  const isGroupMode = Boolean(groupOrigins && groupOrigins.length > 0);
  const showDirectionsButton = !isGroupMode && Boolean(onGetDirections);

  function handleOpenGoogleMaps() {
    const url = `https://www.google.com/maps/dir/?api=1&destination=${place.latitude},${place.longitude}`;
    window.open(url, "_blank", "noopener,noreferrer");
  }

  function handleShare() {
    if (navigator.share) {
      navigator.share({
        title: place.name,
        text: `Địa điểm ${place.name} trên LOSM: ${place.address ?? ""}`,
        url: window.location.href,
      }).catch(() => {});
    } else {
      navigator.clipboard.writeText(`${place.name} - ${place.address ?? ""}`);
      toast.success("Đã sao chép địa chỉ vào bộ nhớ tạm!");
    }
  }

  return (
    <div
      className={`absolute bottom-5 left-1/2 z-[1001] w-[calc(100%-32px)] max-w-lg -translate-x-1/2 rounded-2xl border bg-card/95 shadow-2xl backdrop-blur-md transition-all duration-200 animate-in fade-in-50 slide-in-from-bottom-5 ${
        isCollapsed ? "p-3 sm:py-3 sm:px-4" : "p-4 sm:p-5"
      }`}
    >
      {/* Header */}
      <div className="flex items-center justify-between gap-2">
        <div
          className={`min-w-0 flex-1 ${isCollapsed ? "cursor-pointer select-none" : ""}`}
          onClick={() => isCollapsed && setIsCollapsed(false)}
        >
          <div className="flex items-center gap-2 flex-wrap">
            <h2 className="text-base sm:text-lg font-bold tracking-tight text-foreground truncate">
              {place.name}
            </h2>
            {place.category && (
              <Badge variant="secondary" className="text-[11px] px-2 py-0.5">
                {place.category}
              </Badge>
            )}
            {isCollapsed && place.distanceKm !== undefined && (
              <span className="text-xs text-muted-foreground font-medium">
                • Cách {place.distanceKm.toFixed(1)} km
              </span>
            )}
          </div>

          {!isCollapsed && place.address && (
            <p className="mt-1 text-xs sm:text-sm text-muted-foreground flex items-center gap-1.5 line-clamp-2">
              <MapPin className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
              <span>{place.address}</span>
            </p>
          )}
        </div>

        <div className="flex items-center gap-1 shrink-0">
          <Button
            variant="ghost"
            size="icon"
            onClick={() => setIsCollapsed((prev) => !prev)}
            className="h-8 w-8 text-muted-foreground hover:text-foreground cursor-pointer"
            title={isCollapsed ? "Mở lên" : "Gập xuống"}
            aria-label={isCollapsed ? "Mở lên" : "Gập xuống"}
          >
            {isCollapsed ? (
              <ChevronUp className="h-4 w-4" />
            ) : (
              <ChevronDown className="h-4 w-4" />
            )}
          </Button>

          <Button
            variant="ghost"
            size="icon"
            onClick={onClose}
            className="h-8 w-8 text-muted-foreground hover:text-foreground cursor-pointer"
            title="Đóng thẻ"
            aria-label="Đóng thẻ"
          >
            <X className="h-4 w-4" />
          </Button>
        </div>
      </div>

      {/* Chi tiết nội dung khi không gập */}
      {!isCollapsed && (
        <>
          {/* Info Stats */}
          <div className="mt-3 grid grid-cols-2 sm:grid-cols-3 gap-2 border-y py-2 text-xs">
            {place.distanceKm !== undefined && (
              <div className="flex items-center gap-1.5 text-muted-foreground">
                <Navigation2 className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
                <span>
                  Cách: <strong className="text-foreground">{place.distanceKm.toFixed(1)} km</strong>
                </span>
              </div>
            )}

            {place.openingHours && (
              <div className="flex items-center gap-1.5 text-muted-foreground truncate">
                <Clock className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
                <span className="truncate">{place.openingHours}</span>
              </div>
            )}

            <div className="flex items-center gap-1.5 text-muted-foreground">
              <Compass className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
              <span className="font-mono text-[11px] truncate">
                {place.latitude.toFixed(4)}, {place.longitude.toFixed(4)}
              </span>
            </div>
          </div>

          {/* Khoảng cách từng thành viên hoặc từ mốc tìm kiếm */}
          {groupOrigins && groupOrigins.length > 0 && activeMeetingPlace && (
            <div className="mt-2.5 rounded-xl border border-primary/20 bg-primary/5 p-2.5 text-xs">
              <div className="font-semibold text-primary mb-1.5 flex items-center justify-between text-[11px]">
                <span className="flex items-center gap-1">
                  {groupOrigins.length === 1 && (groupOrigins[0].id === "anchor" || !groupOrigins[0].isMe) ? (
                    <>
                      <MapPin className="h-3.5 w-3.5" />
                      Khoảng cách từ mốc tìm kiếm (bấm để xem đường đi):
                    </>
                  ) : (
                    <>
                      <Users className="h-3.5 w-3.5" />
                      Khoảng cách từng người (bấm để xem đường đi):
                    </>
                  )}
                </span>
                {groupOrigins.length > 1 && activeMeetingPlace.fairnessScore !== undefined && (
                  <span className="text-[10px] text-muted-foreground bg-background/80 px-1.5 py-0.5 rounded border border-border/50">
                    Công bằng: {Math.round(activeMeetingPlace.fairnessScore * 100)}%
                  </span>
                )}
              </div>
              <div className="grid grid-cols-2 gap-1.5 max-h-36 overflow-y-auto pr-1">
                {groupOrigins.map((orig, idx) => {
                  const dist = activeMeetingPlace.distanceKmByOrigin?.[idx];
                  const dur = activeMeetingPlace.durationMinutesByOrigin?.[idx];
                  const isSelected = selectedRouteOriginId === orig.id && navigationRoute;
                  return (
                    <button
                      type="button"
                      key={orig.id}
                      onClick={() => {
                        if (isSelected && onClearDirections) {
                          onClearDirections();
                        } else if (onGetDirections) {
                          onGetDirections(place, orig);
                        }
                      }}
                      className={`flex items-center justify-between text-[11px] px-2 py-1.5 rounded-lg border transition cursor-pointer text-left ${
                        isSelected
                          ? "bg-blue-600 text-white border-blue-600 shadow-sm"
                          : "bg-background/80 hover:bg-muted border-border/50 hover:border-primary/50 text-foreground"
                      }`}
                      title={`Xem đường đi từ ${orig.name} (${orig.address})`}
                    >
                      <div className="flex items-center gap-1 min-w-0">
                        <Navigation2
                          className={`h-3 w-3 shrink-0 ${isSelected ? "text-white" : "text-primary"}`}
                        />
                        <span className="font-semibold truncate max-w-[65px]">
                          {orig.name}
                        </span>
                      </div>
                      <div className="flex items-center gap-1 shrink-0">
                        <span className={`font-bold ${isSelected ? "text-white" : "text-primary"}`}>
                          {dist !== undefined && dist !== null ? `${dist.toFixed(1)} km` : "—"}
                        </span>
                        {dur !== undefined && dur !== null && (
                          <span
                            className={`text-[10px] ${
                              isSelected ? "text-white/80" : "text-muted-foreground"
                            }`}
                          >
                            ({Math.round(dur)}p)
                          </span>
                        )}
                      </div>
                    </button>
                  );
                })}
              </div>
            </div>
          )}

          {/* Real-world Navigation Stats when active */}
          {navigationRoute && (
            <div className="mt-2.5 p-2 px-3 rounded-xl bg-blue-500/10 border border-blue-500/30 flex items-center justify-between text-xs animate-in fade-in">
              <div className="flex items-center gap-1.5 font-semibold text-blue-600 truncate min-w-0">
                <Navigation2 className="h-3.5 w-3.5 shrink-0" />
                <span className="truncate">
                  {selectedOriginName ? `Từ ${selectedOriginName}: ` : ""}
                  Khoảng cách {navigationRoute.distanceKm.toFixed(1)} km (~
                  {Math.round(navigationRoute.durationMinutes)} phút)
                </span>
              </div>
              {onClearDirections && (
                <button
                  type="button"
                  onClick={onClearDirections}
                  className="text-[11px] font-semibold text-blue-600 hover:text-blue-800 underline shrink-0 ml-2 cursor-pointer"
                >
                  Đóng đường đi
                </button>
              )}
            </div>
          )}

          {/* Action Buttons */}
          <div className="mt-3 flex items-center gap-2">
            {showDirectionsButton && (
              <Button
                variant={navigationRoute ? "default" : "outline"}
                size="sm"
                onClick={() => {
                  if (navigationRoute && onClearDirections) {
                    onClearDirections();
                  } else {
                    onGetDirections?.(place);
                  }
                }}
                disabled={loadingDirections}
                className={`flex-1 gap-1.5 text-xs h-9 font-medium cursor-pointer ${
                  navigationRoute ? "bg-blue-600 hover:bg-blue-700 text-white" : ""
                }`}
              >
                {loadingDirections ? (
                  <Loader2 className="h-3.5 w-3.5 animate-spin" />
                ) : (
                  <Navigation2 className="h-3.5 w-3.5" />
                )}
                <span>
                  {navigationRoute ? "Đóng đường đi" : "Chỉ đường (từ GPS)"}
                </span>
              </Button>
            )}

            {onScheduleMeeting && (
              <Button
                size="sm"
                onClick={() => onScheduleMeeting(place)}
                className="flex-1 gap-1.5 text-xs h-9 font-medium cursor-pointer bg-black text-white hover:bg-neutral-800 active:scale-[0.98] transition-all shadow-sm"
              >
                <Calendar className="h-3.5 w-3.5" />
                Hẹn bạn bè tại đây
              </Button>
            )}

            {/* Secondary: Open Google Maps */}
            <Button
              variant="outline"
              size="icon"
              onClick={handleOpenGoogleMaps}
              title="Mở trên Google Maps"
              className="h-9 w-9 text-muted-foreground hover:text-foreground shrink-0 cursor-pointer"
            >
              <ExternalLink className="h-3.5 w-3.5" />
            </Button>

            <Button
              variant="ghost"
              size="icon"
              onClick={handleShare}
              title="Chia sẻ"
              className="h-9 w-9 text-muted-foreground hover:text-foreground shrink-0 cursor-pointer"
            >
              <Share2 className="h-4 w-4" />
            </Button>
          </div>
        </>
      )}
    </div>
  );
}
