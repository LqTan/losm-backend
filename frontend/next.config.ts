import type { NextConfig } from "next";

const apiBase =
  process.env.INTERNAL_API_URL ??
  process.env.NEXT_PUBLIC_API_BASE_URL ??
  "http://localhost:5232";

const nextConfig: NextConfig = {
  output: "standalone",
  experimental: { proxyClientMaxBodySize: "27mb" },
  async rewrites() {
    return [
      {
        source: "/api/admin/transit/:path*",
        destination: `${apiBase}/api/admin/transit/:path*`,
      },
      {
        source: "/api/admin/hangfire-proxy",
        destination: `${apiBase}/api/admin/hangfire-proxy`,
      },
      {
        source: "/api/admin/hangfire-proxy/",
        destination: `${apiBase}/api/admin/hangfire-proxy/`,
      },
      {
        source: "/api/admin/hangfire-proxy/:path*",
        destination: `${apiBase}/api/admin/hangfire-proxy/:path*`,
      },
      {
        source: "/hangfire",
        destination: `${apiBase}/api/admin/hangfire-proxy`,
      },
      {
        source: "/hangfire/:path*",
        destination: `${apiBase}/api/admin/hangfire-proxy/:path*`,
      },
      {
        source: "/api/admin/ranking-model/:path*",
        destination: `${apiBase}/api/admin/ranking-model/:path*`,
      },
      {
        source: "/api/admin/category-mappings",
        destination: `${apiBase}/api/admin/category-mappings`,
      },
      {
        source: "/api/admin/category-mappings/:path*",
        destination: `${apiBase}/api/admin/category-mappings/:path*`,
      },
    ];
  },
};

export default nextConfig;