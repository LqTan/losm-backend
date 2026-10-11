/**
 * Cấu hình Tile Layer bản đồ dùng chung toàn ứng dụng.
 * Có thể ghi đè thông qua biến môi trường (.env / .env.local):
 * - NEXT_PUBLIC_MAP_TILE_URL
 * - NEXT_PUBLIC_MAP_TILE_ATTRIBUTION
 * - NEXT_PUBLIC_MAP_TILE_SUBDOMAINS (cách nhau bởi dấu phẩy, vd: a,b,c,d)
 */
export const MAP_CONFIG = {
  tileUrl:
    process.env.NEXT_PUBLIC_MAP_TILE_URL ||
    "https://{s}.basemaps.cartocdn.com/rastertiles/voyager/{z}/{x}/{y}{r}.png",
  attribution:
    process.env.NEXT_PUBLIC_MAP_TILE_ATTRIBUTION ||
    '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors &copy; <a href="https://carto.com/attributions">CARTO</a>',
  subdomains: process.env.NEXT_PUBLIC_MAP_TILE_SUBDOMAINS
    ? process.env.NEXT_PUBLIC_MAP_TILE_SUBDOMAINS.split(",").map((s) => s.trim())
    : ["a", "b", "c", "d"],
  maxZoom: 20,
};
