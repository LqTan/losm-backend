"use client";

import { useEffect, useState } from "react";
import { GeoJSON, MapContainer, Rectangle, TileLayer, useMap } from "react-leaflet";
import "leaflet/dist/leaflet.css";

export type Bbox = {
  minLat: number;
  minLng: number;
  maxLat: number;
  maxLng: number;
};

const DEFAULT_CENTER: [number, number] = [10.7709, 106.7009];

type Props = {
  value: Bbox;
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  geoJson?: any;
  onChange?: (b: Bbox) => void;
};

function MapBoundsUpdater({ bounds }: { bounds: [[number, number], [number, number]] }) {
  const map = useMap();
  useEffect(() => {
    if (
      bounds &&
      !isNaN(bounds[0][0]) &&
      !isNaN(bounds[1][0]) &&
      Math.abs(bounds[1][0] - bounds[0][0]) > 0.0001 &&
      Math.abs(bounds[1][1] - bounds[0][1]) > 0.0001
    ) {
      map.fitBounds(bounds, { padding: [30, 30], maxZoom: 14 });
    }
  }, [bounds, map]);
  return null;
}

export default function BboxPickerMap({ value, geoJson }: Props) {
  const [mounted, setMounted] = useState(false);

  useEffect(() => setMounted(true), []);

  if (!mounted) {
    return <div className="h-[380px] w-full rounded-md border bg-muted" />;
  }

  const bounds: [[number, number], [number, number]] = [
    [value.minLat, value.minLng],
    [value.maxLat, value.maxLng],
  ];

  const geoJsonKey = geoJson ? JSON.stringify(geoJson).length + "-" + value.minLat : "no-geojson";

  return (
    <div className="relative overflow-hidden rounded-md border">
      <MapContainer
        center={DEFAULT_CENTER}
        zoom={11}
        className="h-[380px] w-full"
        scrollWheelZoom
      >
        <TileLayer
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a>'
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
        />

        {/* Auto fit bounds when coordinates update */}
        <MapBoundsUpdater bounds={bounds} />

        {/* Calculated Bounding Box Rectangle (Red dashed outline) */}
        <Rectangle
          bounds={bounds}
          pathOptions={{
            color: "#ef4444",
            dashArray: "5, 5",
            weight: 1.5,
            fillColor: "#ef4444",
            fillOpacity: 0.05,
          }}
        />

        {/* Exact GeoJSON boundary with curves and actual district shape (Blue) */}
        {Boolean(geoJson) && (
          <GeoJSON
            key={geoJsonKey}
            // eslint-disable-next-line @typescript-eslint/no-explicit-any
            data={geoJson as any}
            style={{
              color: "#2563eb",
              weight: 2.5,
              fillColor: "#3b82f6",
              fillOpacity: 0.22,
            }}
          />
        )}
      </MapContainer>
    </div>
  );
}