"use client";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import type { Place, MeetingPlace } from "@/types/place";
import type { OriginItem } from "@/components/meeting/GroupMeetingFinder";
import {
  Calendar,
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
  onClose: () => void;
  onScheduleMeeting?: (place: Place) => void;
  onGetDirections?: (place: Place) => void;
  onClearDirections?: () => void;
}

export default function PlaceCard({
  place,
  activeMeetingPlace,
  groupOrigins,
  navigationRoute,
  loadingDirections,
  onClose,
  onScheduleMeeting,
  onGetDirections,
  onClearDirections,
}: Props) {
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
    <div className="absolute bottom-5 left-1/2 z-[1001] w-[calc(100%-32px)] max-w-lg -translate-x-1/2 rounded-2xl border bg-card/95 p-4 sm:p-5 shadow-2xl backdrop-blur-md transition-all animate-in fade-in-50 slide-in-from-bottom-5">
      {/* Header */}
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2 flex-wrap">
            <h2 className="text-lg sm:text-xl font-bold tracking-tight text-foreground truncate">
              {place.name}
            </h2>
            {place.category && (
              <Badge variant="secondary" className="text-xs px-2 py-0.5">
                {place.category}
              </Badge>
            )}
          </div>

          {place.address && (
            <p className="mt-1 text-xs sm:text-sm text-muted-foreground flex items-center gap-1.5 line-clamp-2">
              <MapPin className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
              <span>{place.address}</span>
            </p>
          )}
        </div>

        <Button
          variant="ghost"
          size="icon"
          onClick={onClose}
          className="h-8 w-8 text-muted-foreground hover:text-foreground shrink-0"
        >
          <X className="h-4 w-4" />
        </Button>
      </div>

      {/* Info Stats */}
      <div className="mt-3 grid grid-cols-2 sm:grid-cols-3 gap-2 border-y py-2 text-xs">
        {place.distanceKm !== undefined && (
          <div className="flex items-center gap-1.5 text-muted-foreground">
            <Navigation2 className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
            <span>Cách: <strong className="text-foreground">{place.distanceKm.toFixed(1)} km</strong></span>
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

      {/* Khoảng cách từng thành viên khi đang trong chế độ điểm hẹn */}
      {groupOrigins && groupOrigins.length > 0 && activeMeetingPlace && (
        <div className="mt-2.5 rounded-xl border border-primary/20 bg-primary/5 p-2.5 text-xs">
          <div className="font-semibold text-primary mb-1.5 flex items-center justify-between text-[11px]">
            <span className="flex items-center gap-1">
              <Users className="h-3.5 w-3.5" />
              Khoảng cách từng người đến đây:
            </span>
            {activeMeetingPlace.fairnessScore !== undefined && (
              <span className="text-[10px] text-muted-foreground bg-background/80 px-1.5 py-0.5 rounded border border-border/50">
                Công bằng: {Math.round(activeMeetingPlace.fairnessScore * 100)}%
              </span>
            )}
          </div>
          <div className="grid grid-cols-2 gap-1.5">
            {groupOrigins.map((orig, idx) => {
              const dist = activeMeetingPlace.distanceKmByOrigin?.[idx];
              const dur = activeMeetingPlace.durationMinutesByOrigin?.[idx];
              return (
                <div
                  key={orig.id}
                  className="flex items-center justify-between text-[11px] bg-background/70 px-2 py-1 rounded-md border border-border/40"
                >
                  <span className="font-semibold text-foreground truncate max-w-[65px]">
                    {orig.name}
                  </span>
                  <div className="flex items-center gap-1 shrink-0">
                    <span className="text-primary font-bold">
                      {dist !== undefined && dist !== null ? `${dist.toFixed(1)} km` : "—"}
                    </span>
                    {dur !== undefined && dur !== null && (
                      <span className="text-muted-foreground text-[10px]">
                        ({Math.round(dur)}p)
                      </span>
                    )}
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      )}

      {/* Real-world Navigation Stats when active */}
      {navigationRoute && (
        <div className="mt-2.5 p-2 rounded-xl bg-blue-500/10 border border-blue-500/30 flex items-center justify-between text-xs animate-in fade-in">
          <div className="flex items-center gap-1.5 font-semibold text-blue-600">
            <Navigation2 className="h-3.5 w-3.5" />
            <span>Khoảng cách: {navigationRoute.distanceKm.toFixed(1)} km</span>
          </div>
          <span className="text-muted-foreground text-[11px] font-medium">
            Thời gian lái xe: ~{Math.round(navigationRoute.durationMinutes)} phút
          </span>
        </div>
      )}

      {/* Action Buttons */}
      <div className="mt-3 flex items-center gap-2">
        {onGetDirections && (
          <Button
            variant={navigationRoute ? "default" : "outline"}
            size="sm"
            onClick={() => {
              if (navigationRoute && onClearDirections) {
                onClearDirections();
              } else {
                onGetDirections(place);
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
            <span>{navigationRoute ? "Đóng đường đi" : "Chỉ đường"}</span>
          </Button>
        )}

        {onScheduleMeeting && (
          <Button
            size="sm"
            onClick={() => onScheduleMeeting(place)}
            className="flex-1 gap-1.5 text-xs h-9 font-medium cursor-pointer"
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
    </div>
  );
}
