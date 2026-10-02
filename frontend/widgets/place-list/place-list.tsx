"use client";

import type { Place } from "@/entities/place/model";
import { PlaceCard } from "../place-card";
import { EmptyState } from "@/shared/ui/empty-state";

export interface PlaceListProps {
  readonly places: readonly Place[];
  readonly onSelect?: (place: Place) => void;
  readonly onSave?: (place: Place) => void;
  readonly highlightedId?: string | null;
  readonly savedIds?: ReadonlySet<string>;
  readonly emptyMessage?: string;
  readonly savingId?: string | null;
}

export function PlaceList({
  places,
  onSelect,
  onSave,
  highlightedId,
  savedIds,
  emptyMessage = "Không có địa điểm.",
  savingId,
}: PlaceListProps) {
  if (places.length === 0) {
    return <EmptyState title={emptyMessage} />;
  }
  return (
    <ul className="flex flex-col gap-2">
      {places.map((p) => (
        <li key={p.id}>
          <PlaceCard
            place={p}
            {...(onSelect ? { onSelect } : {})}
            {...(onSave ? { onSave } : {})}
            highlighted={highlightedId === p.id}
            {...(savedIds ? { saved: savedIds.has(p.id) } : {})}
            saving={savingId === p.id}
          />
        </li>
      ))}
    </ul>
  );
}
