"use client";

import { MapPin, Star, Bookmark } from "lucide-react";
import type { Place } from "@/entities/place/model";
import { Card } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  formatDistance,
  formatRating,
  formatScore,
} from "@/shared/lib/formatters";
import { cn } from "@/shared/lib/cn";

export interface PlaceCardProps {
  readonly place: Place;
  readonly highlighted?: boolean;
  readonly onSelect?: (place: Place) => void;
  readonly onSave?: (place: Place) => void;
  readonly saving?: boolean;
  readonly saved?: boolean;
}

export function PlaceCard({
  place,
  highlighted,
  onSelect,
  onSave,
  saving,
  saved,
}: PlaceCardProps) {
  return (
    <Card
      className={cn(
        "p-3 transition-colors",
        highlighted && "ring-2 ring-foreground/30",
      )}
    >
      <div className="flex items-start justify-between gap-2">
        <button
          type="button"
          onClick={() => onSelect?.(place)}
          className="flex-1 min-w-0 text-left"
          aria-label={`Mở chi tiết ${place.name}`}
        >
          <p className="text-[14px] font-semibold line-clamp-1">{place.name}</p>
          <p className="mt-0.5 text-[12px] text-muted-foreground line-clamp-1">
            {place.address}
          </p>
        </button>
        {onSave ? (
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            aria-label={saved ? "Đã lưu" : "Lưu"}
            onClick={() => onSave(place)}
            disabled={saving}
          >
            <Bookmark
              size={14}
              strokeWidth={1.5}
              fill={saved ? "currentColor" : "none"}
            />
          </Button>
        ) : null}
      </div>
      <div className="mt-2 flex flex-wrap items-center gap-2 text-[12px]">
        <span className="inline-flex items-center gap-1 text-foreground">
          <MapPin size={12} strokeWidth={1.5} aria-hidden />
          {formatDistance(place.distanceKm)}
        </span>
        <span className="inline-flex items-center gap-1 text-foreground">
          <Star size={12} strokeWidth={1.5} aria-hidden />
          {formatRating(place.rating)}
        </span>
        {typeof place.finalScore === "number" ? (
          <Badge variant="secondary" className="font-mono">
            {formatScore(place.finalScore)}
          </Badge>
        ) : null}
        {place.categories[0] ? (
          <Badge variant="outline">{place.categories[0]}</Badge>
        ) : null}
      </div>
    </Card>
  );
}
