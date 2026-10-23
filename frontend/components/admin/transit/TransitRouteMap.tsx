"use client";

import { useEffect } from "react";
import { CircleMarker, MapContainer, Polyline, Tooltip, useMap } from "react-leaflet";
import AppTileLayer from "@/components/map/AppTileLayer";
import type { TransitStop } from "@/lib/transit";
import "leaflet/dist/leaflet.css";

function FitStops({ stops }: { stops: TransitStop[] }) {
  const map = useMap();
  useEffect(() => {
    if (stops.length) map.fitBounds(stops.map(s => [s.latitude, s.longitude] as [number, number]), { padding: [30, 30], maxZoom: 16 });
  }, [map, stops]);
  return null;
}
export default function TransitRouteMap({ stops }: { stops: TransitStop[] }) {
  if (!stops.length) return <p className="p-4 text-muted-foreground">Không có trạm để hiển thị.</p>;
  return <div>
    <p className="mb-2 text-sm text-muted-foreground">Sơ đồ nối trạm theo thứ tự, không phải đường chạy thực tế. Màu đỏ: trạm đang bị đánh dấu dữ liệu sai.</p>
    <MapContainer center={[stops[0].latitude, stops[0].longitude]} zoom={12} className="h-[360px] w-full rounded-lg" scrollWheelZoom>
      <AppTileLayer />
      <FitStops stops={stops} />
      <Polyline positions={stops.map(s => [s.latitude, s.longitude] as [number, number])} pathOptions={{ color: "#2563eb", dashArray: "6 5" }} />
      {stops.map((s, i) => <CircleMarker key={`${s.id}-${i}`} center={[s.latitude, s.longitude]} radius={6} pathOptions={{ color: s.isNotSafe ? "#dc2626" : "#2563eb", fillOpacity: 0.85 }}>
        <Tooltip>{s.sequence ?? i + 1}. {s.name}{s.isNotSafe ? " — Dữ liệu sai" : ""}</Tooltip>
      </CircleMarker>)}
    </MapContainer>
  </div>;
}
