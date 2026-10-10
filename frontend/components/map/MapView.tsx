"use client";

import {
  CircleMarker,
  MapContainer,
  Popup,
  TileLayer,
  useMap,
} from "react-leaflet";
import type { Place } from "@/types/place";
import { useEffect } from "react";
import "leaflet/dist/leaflet.css";

const DEFAULT_CENTER: [number, number] = [10.7769, 106.7009];

interface Props {
  places: Place[];
  selectedPlace: Place | null;
  userLocation: { lat: number; lng: number } | null;
  onSelectPlace: (place: Place) => void;
  flyToTarget?: { lat: number; lng: number } | null;
}

function MapViewController({
  target,
}: {
  target?: { lat: number; lng: number } | null;
}) {
  const map = useMap();

  useEffect(() => {
    if (target && target.lat && target.lng) {
      map.flyTo([target.lat, target.lng], 15, {
        duration: 1.2,
      });
    }
  }, [target, map]);

  return null;
}

export default function MapView({
  places,
  selectedPlace,
  userLocation,
  onSelectPlace,
  flyToTarget,
}: Props) {
  const centerPos = userLocation
    ? ([userLocation.lat, userLocation.lng] as [number, number])
    : DEFAULT_CENTER;

  const activeTarget = flyToTarget ?? (selectedPlace ? { lat: selectedPlace.latitude, lng: selectedPlace.longitude } : null);

  return (
    <MapContainer
      center={centerPos}
      zoom={14}
      zoomControl={false}
      className="h-full w-full"
    >
      <TileLayer
        attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
      />

      <MapViewController target={activeTarget} />

      {/* User Current Location Marker */}
      {userLocation && (
        <CircleMarker
          center={[userLocation.lat, userLocation.lng]}
          radius={8}
          pathOptions={{
            color: "#ffffff",
            weight: 2.5,
            fillColor: "#0284c7",
            fillOpacity: 0.9,
          }}
        >
          <Popup>
            <div className="text-xs font-semibold p-1">
              📍 Vị trí hiện tại của bạn
            </div>
          </Popup>
        </CircleMarker>
      )}

      {/* Place Markers */}
      {places.map((place) => {
        const isSelected = selectedPlace?.id === place.id;
        const color = isSelected ? "#0f172a" : "#2563eb";

        return (
          <CircleMarker
            key={place.id}
            center={[place.latitude, place.longitude]}
            radius={isSelected ? 12 : 8}
            pathOptions={{
              color: isSelected ? "#ffffff" : color,
              weight: isSelected ? 3 : 1.5,
              fillColor: color,
              fillOpacity: isSelected ? 1 : 0.85,
            }}
            eventHandlers={{
              click: () => onSelectPlace(place),
            }}
          >
            <Popup>
              <div className="p-1 space-y-1 text-xs">
                <p className="font-bold text-sm text-gray-900 leading-tight">
                  {place.name}
                </p>
                {place.category && (
                  <p className="text-gray-500 font-medium">
                    {place.category}
                  </p>
                )}
                {place.address && (
                  <p className="text-gray-600 line-clamp-2">
                    {place.address}
                  </p>
                )}
                {place.distanceKm !== undefined && (
                  <p className="text-blue-600 font-semibold pt-0.5">
                    Cách bạn: {place.distanceKm.toFixed(1)} km
                  </p>
                )}
                <button
                  type="button"
                  onClick={() => onSelectPlace(place)}
                  className="mt-1 w-full rounded-md bg-blue-600 py-1 text-[11px] font-semibold text-white hover:bg-blue-700 transition"
                >
                  Xem chi tiết
                </button>
              </div>
            </Popup>
          </CircleMarker>
        );
      })}
    </MapContainer>
  );
}
