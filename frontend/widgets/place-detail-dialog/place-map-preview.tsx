"use client";

import { MapPanel } from "@/widgets/map-panel/map-panel";

export interface PlaceMapPreviewProps {
  readonly center: { lat: number; lon: number };
  readonly places: Parameters<typeof MapPanel>[0]["places"];
  readonly highlightedId?: string | null;
}

export function PlaceMapPreview({
  center,
  places,
  highlightedId,
}: PlaceMapPreviewProps) {
  return (
    <div className="h-[200px] overflow-hidden rounded-md border">
      <MapPanel
        center={center}
        places={places}
        {...(highlightedId !== undefined ? { highlightedId } : {})}
      />
    </div>
  );
}
