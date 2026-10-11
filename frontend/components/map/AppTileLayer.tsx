"use client";

import { TileLayer } from "react-leaflet";
import { MAP_CONFIG } from "@/lib/mapConfig";

/**
 * Component TileLayer dùng chung cho toàn bộ các màn hình bản đồ trong hệ thống.
 * Tự động đọc cấu hình từ .env hoặc fallback về CartoDB Voyager.
 */
export default function AppTileLayer() {
  return (
    <TileLayer
      attribution={MAP_CONFIG.attribution}
      url={MAP_CONFIG.tileUrl}
      subdomains={MAP_CONFIG.subdomains}
      maxZoom={MAP_CONFIG.maxZoom}
    />
  );
}
