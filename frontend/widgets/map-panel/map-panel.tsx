"use client";

import { useEffect, useRef } from "react";
import L from "leaflet";
import {
  MapContainer,
  Marker,
  Popup,
  TileLayer,
  useMap,
} from "react-leaflet";
import {
  MAP_ATTRIBUTION,
  MAP_DEFAULT_ZOOM,
  MAP_MAX_ZOOM,
  MAP_MIN_ZOOM,
  MAP_TILE_URL,
} from "@/shared/config/map";
import type { GeoPoint } from "@/entities/place/model";
import type { PlaceId } from "@/shared/lib/brands";

export interface MapPlace {
  readonly id: PlaceId;
  readonly name: string;
  readonly address: string | null;
  readonly location: GeoPoint;
}

const defaultIcon = L.divIcon({
  className: "losm-marker",
  html: '<span style="display:block;width:12px;height:12px;border-radius:999px;background:#000;border:2px solid #fff"></span>',
  iconSize: [16, 16],
  iconAnchor: [8, 8],
});

function FlyToHighlight({
  places,
  highlightedId,
}: {
  places: readonly MapPlace[];
  highlightedId?: string | null;
}) {
  const map = useMap();
  const lastIdRef = useRef<string | null>(null);
  useEffect(() => {
    if (!highlightedId || highlightedId === lastIdRef.current) return;
    const place = places.find((p) => p.id === highlightedId);
    if (!place) return;
    lastIdRef.current = highlightedId;
    map.flyTo([place.location.lat, place.location.lon], MAP_DEFAULT_ZOOM, {
      duration: 0.6,
    });
  }, [highlightedId, places, map]);
  return null;
}

export interface MapPanelProps {
  readonly places: readonly MapPlace[];
  readonly center: GeoPoint;
  readonly zoom?: number;
  readonly highlightedId?: string | null;
  readonly onSelect?: (place: MapPlace) => void;
}

export function MapPanel({
  places,
  center,
  zoom = MAP_DEFAULT_ZOOM,
  highlightedId,
  onSelect,
}: MapPanelProps) {
  return (
    <MapContainer
      center={[center.lat, center.lon]}
      zoom={zoom}
      minZoom={MAP_MIN_ZOOM}
      maxZoom={MAP_MAX_ZOOM}
      className="h-full w-full"
      attributionControl
    >
      <TileLayer url={MAP_TILE_URL} attribution={MAP_ATTRIBUTION} />
      {places.map((p) => (
        <Marker
          key={p.id}
          position={[p.location.lat, p.location.lon]}
          icon={defaultIcon}
          eventHandlers={{
            click: () => onSelect?.(p),
          }}
        >
          <Popup>
            <strong>{p.name}</strong>
            {p.address ? <div>{p.address}</div> : null}
          </Popup>
        </Marker>
      ))}
      <FlyToHighlight
        places={places}
        {...(highlightedId !== undefined ? { highlightedId } : {})}
      />
    </MapContainer>
  );
}
