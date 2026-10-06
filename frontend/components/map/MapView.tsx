"use client";

import {
    CircleMarker,
    MapContainer,
    TileLayer,
} from "react-leaflet";

import type { Place } from "@/types/place";

const DEFAULT_CENTER: [number, number] = [
    10.7709,
    106.7009,
];

type Props = {
    places: Place[];
    onSelectPlace: (place: Place) => void;
};

export default function MapView({
    places,
    onSelectPlace,
}: Props) {
    return (
        <MapContainer
            center={DEFAULT_CENTER}
            zoom={14}
            zoomControl={false}
            className="h-full w-full"
        >
            <TileLayer
                attribution="&copy; OpenStreetMap contributors"
                url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
            />

            {places.map((place) => (
                <CircleMarker
                    key={place.id}
                    center={[
                        place.latitude,
                        place.longitude,
                    ]}
                    radius={9}
                    eventHandlers={{
                        click: () => onSelectPlace(place),
                    }}
                />
            ))}
        </MapContainer>
    );
}
