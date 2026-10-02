"use client";

import dynamic from "next/dynamic";
import { MapPin, Save, CalendarClock, Clock } from "lucide-react";
import type { AttachedPlace } from "@/entities/chat-session/model";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Hairline } from "@/shared/ui/hairline";
import { COPY } from "@/shared/config/copy";
import { cn } from "@/shared/lib/cn";

const PlaceMapPreview = dynamic(
  () => import("./place-map-preview").then((m) => m.PlaceMapPreview),
  {
    ssr: false,
    loading: () => (
      <div className="h-[200px] animate-pulse rounded-md bg-muted" />
    ),
  },
);

export interface PlaceDetailDialogProps {
  readonly place: AttachedPlace | null;
  readonly onClose: () => void;
  readonly onSave?: (place: AttachedPlace) => Promise<void> | void;
  readonly onBook?: (place: AttachedPlace) => void;
  readonly saving?: boolean;
}

export function PlaceDetailDialog({
  place,
  onClose,
  onSave,
  onBook,
  saving,
}: PlaceDetailDialogProps) {
  const detail = COPY.place.detail;
  return (
    <Dialog
      open={place !== null}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
    >
      <DialogContent className="max-w-lg">
        {place ? (
          <>
            <DialogHeader>
              <DialogTitle>{place.name}</DialogTitle>
            </DialogHeader>
            <div className="flex flex-col gap-4 p-4">
              <div className="flex items-start gap-2 text-[13px] text-muted-foreground">
                <MapPin
                  size={14}
                  strokeWidth={1.5}
                  className="mt-0.5 shrink-0"
                  aria-hidden
                />
                <p className="leading-[1.5]">{place.address ?? detail.dash}</p>
              </div>

              <div className="flex flex-wrap items-center gap-x-4 gap-y-1 text-[13px]">
                {place.distanceKm !== null ? (
                  <span className="inline-flex items-center gap-1">
                    <MapPin size={14} strokeWidth={1.5} aria-hidden />
                    {detail.distanceKm(place.distanceKm)}
                  </span>
                ) : null}
                {place.finalScore !== null ? (
                  <span>{detail.score(place.finalScore)}</span>
                ) : null}
                {place.category ? (
                  <span className="text-muted-foreground">{place.category}</span>
                ) : null}
              </div>

              {place.openingHours ? (
                <div className="flex items-start gap-2 text-[13px]">
                  <Clock
                    size={14}
                    strokeWidth={1.5}
                    className="mt-0.5 shrink-0"
                    aria-hidden
                  />
                  <p className="leading-[1.5]">{place.openingHours}</p>
                </div>
              ) : null}

              <Hairline />

              <PlaceMapPreview
                center={place.location}
                places={[
                  {
                    id: place.id,
                    name: place.name,
                    address: place.address,
                    location: place.location,
                  },
                ]}
                highlightedId={place.id}
              />

              <div
                className={cn(
                  "grid gap-2",
                  onBook && onSave ? "grid-cols-2" : "grid-cols-1",
                )}
              >
                {onBook ? (
                  <Button
                    onClick={() => onBook(place)}
                    className="w-full"
                    type="button"
                  >
                    <CalendarClock size={16} strokeWidth={1.5} />
                    {COPY.place.book}
                  </Button>
                ) : null}
                {onSave ? (
                  <Button
                    onClick={() => void onSave(place)}
                    type="button"
                    variant="secondary"
                    className="w-full"
                    disabled={saving}
                  >
                    <Save size={16} strokeWidth={1.5} />
                    {COPY.place.save}
                  </Button>
                ) : null}
              </div>

              <p className="text-[11px] text-muted-foreground">
                {place.category?.startsWith("amenity/")
                  ? detail.sourceOsm
                  : detail.sourceSystem}
              </p>
            </div>
          </>
        ) : null}
      </DialogContent>
    </Dialog>
  );
}
