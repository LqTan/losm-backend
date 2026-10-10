import type { NextConfig } from "next";

const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL ?? "http://localhost:5232";

const nextConfig: NextConfig = {
  async rewrites() {
    return [
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
    ];
  },
};

export default nextConfig;