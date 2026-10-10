"use client";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import type { Place } from "@/types/place";
import {
  Calendar,
  Clock,
  Compass,
  MapPin,
  Navigation2,
  Share2,
  X,
} from "lucide-react";
import { toast } from "sonner";

interface Props {
  place: Place;
  onClose: () => void;
  onScheduleMeeting?: (place: Place) => void;
}

export default function PlaceCard({ place, onClose, onScheduleMeeting }: Props) {
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

      {/* Action Buttons */}
      <div className="mt-3 flex items-center gap-2">
        <Button
          variant="outline"
          size="sm"
          onClick={handleOpenGoogleMaps}
          className="flex-1 gap-1.5 text-xs h-9 font-medium"
        >
          <Navigation2 className="h-3.5 w-3.5 text-muted-foreground" />
          Chỉ đường
        </Button>

        {onScheduleMeeting && (
          <Button
            size="sm"
            onClick={() => onScheduleMeeting(place)}
            className="flex-1 gap-1.5 text-xs h-9 font-medium"
          >
            <Calendar className="h-3.5 w-3.5" />
            Hẹn bạn bè tại đây
          </Button>
        )}

        <Button
          variant="ghost"
          size="icon"
          onClick={handleShare}
          className="h-9 w-9 text-muted-foreground hover:text-foreground shrink-0"
        >
          <Share2 className="h-4 w-4" />
        </Button>
      </div>
    </div>
  );
}
