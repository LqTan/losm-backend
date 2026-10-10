"use client";

import SearchResults from "./SearchResults";
import type { Place } from "@/types/place";

type Props = {
    places: Place[];
    onSelectPlace: (place: Place) => void;
};

export default function ResultsSidebar({
    places,
    onSelectPlace,
}: Props) {
    return (
        <aside className="absolute left-4 top-20 bottom-4 z-[1000] flex w-[360px] max-w-[calc(100vw-32px)] flex-col overflow-hidden rounded-2xl bg-white shadow-[0_8px_28px_rgba(0,0,0,0.18)]">
            <div className="min-h-0 flex-1 overflow-y-auto">
                <SearchResults
                    places={places}
                    onSelectPlace={onSelectPlace}
                />
            </div>
        </aside>
    );
}