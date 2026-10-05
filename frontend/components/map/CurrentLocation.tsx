"use client";

import { LocateFixed } from "lucide-react";
import { useState } from "react";
import { CircleMarker, useMap } from "react-leaflet";

type Position = {
    latitude: number;
    longitude: number;
};

export default function CurrentLocation() {
    const map = useMap();
    const [position, setPosition] = useState<Position | null>(null);

    function locateUser() {
        if (!navigator.geolocation) {
            console.error("Geolocation is not supported.");
            return;
        }

        navigator.geolocation.getCurrentPosition(
            (result) => {
                const latitude = result.coords.latitude;
                const longitude = result.coords.longitude;

                setPosition({
                    latitude,
                    longitude
                });
                map.flyTo([latitude, longitude], 16);
            },
            (error) => {
                console.error("Cannot get current location:", error);
            },
            {
                enableHighAccuracy: true,
                timeout: 10000,
            }
        );
    }

    return (
        <>
            {position && (
                <CircleMarker
                    center={[position.latitude, position.longitude]}
                    radius={8}
                    pathOptions={{
                        color: "white",
                        weight: 3,
                        fillColor: "#2563eb",
                        fillOpacity: 1,
                    }}
                />
            )}

            <button
                type="button"
                onClick={locateUser}
                className="absolute bottom-8 right-5 z-[1000] flex h-12 w-12 items-center justify-center rounded-full bg-white shadow-lg transition hover:bg-gray-50 cursor-pointer"
                aria-label="Vị trí của tôi"
            >
                <LocateFixed className="h-5 w-5 text-gray-700" />
            </button>
        </>
    );
}
