"use client"
import type { Place } from "@/types/place";

type Props = {
    place: Place;
    onClose: () => void;
};

export default function PlaceCard({ place, onClose }: Props) {
    return (
        <div className="absolute bottom-5 left-1/2 z-[1000] w-[calc(100%-32px)] max-w-md -translate-x-1/2 rounded-2xl bg-white p-4 shadow-xl">
            <button
                onClick={onClose}
                className="float-right cursor-pointer text-gray-500"
            >x</button>

            <h2 className="text-lg font-semibold">{place.name}</h2>
            <p className="text-sm text-gray-500">{place.category}</p>
        </div>
    )
}
