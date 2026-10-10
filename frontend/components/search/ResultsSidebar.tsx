"use client";

import SearchResults from "./SearchResults";
import type { Place } from "@/types/place";

type Props = {
  places: Place[];
  onSelectPlace: (place: Place) => void;
};

export default function ResultsSidebar({ places, onSelectPlace }: Props) {
  return (
    <div className="min-h-0 flex-1 overflow-y-auto">
      <SearchResults places={places} onSelectPlace={onSelectPlace} />
    </div>
  );
}