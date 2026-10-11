/**
 * Cấu hình Tile Layer bản đồ dùng chung toàn ứng dụng.
 * Sử dụng OpenStreetMap HOT (Humanitarian OSM - máy chủ tilecache Châu Âu / Pháp)
 * - Hoàn toàn MIỄN PHÍ, KHÔNG cần API key, KHÔNG bị watermark.
 * - Tên miền `tile.openstreetmap.fr` phân giải thông suốt tại tất cả nhà mạng VN.
 * - Có thể ghi đè qua biến môi trường NEXT_PUBLIC_MAP_TILE_* trong .env nếu cần.
 */
export const MAP_CONFIG = {
  tileUrl:
    process.env.NEXT_PUBLIC_MAP_TILE_URL ||
    "https://{s}.tile.openstreetmap.fr/hot/{z}/{x}/{y}.png",
  attribution:
    process.env.NEXT_PUBLIC_MAP_TILE_ATTRIBUTION ||
    '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors, Tiles style by <a href="https://www.hotosm.org/" target="_blank">HOT</a>',
  subdomains: process.env.NEXT_PUBLIC_MAP_TILE_SUBDOMAINS
    ? process.env.NEXT_PUBLIC_MAP_TILE_SUBDOMAINS.split(",").map((s) => s.trim())
    : ["a", "b", "c"],
  maxZoom: 19,
};
