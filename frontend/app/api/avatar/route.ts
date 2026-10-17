import { NextRequest, NextResponse } from "next/server";

export const dynamic = "force-dynamic";

export async function GET(request: NextRequest) {
  const { searchParams } = new URL(request.url);
  const url = searchParams.get("url");

  if (url && (url.startsWith("https://") || url.startsWith("http://"))) {
    try {
      const response = await fetch(url, {
        headers: {
          "User-Agent":
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36",
          Accept: "image/avif,image/webp,image/apng,image/svg+xml,image/*,*/*;q=0.8",
        },
      });

      if (response.ok) {
        const contentType =
          response.headers.get("content-type") || "image/jpeg";
        const arrayBuffer = await response.arrayBuffer();

        return new NextResponse(arrayBuffer, {
          headers: {
            "Content-Type": contentType,
            "Cache-Control": "public, max-age=86400, stale-while-revalidate=604800",
          },
        });
      }
    } catch {
      // Fall through to fallback SVG
    }
  }

  // Fallback: Generate a crisp circular SVG avatar with initial
  const name = searchParams.get("name") || "Tôi";
  let initial = name.trim().charAt(0).toUpperCase() || "T";
  const numMatch = name.match(/\d+/);
  if (name.toLowerCase().startsWith("người") && numMatch) {
    initial = numMatch[0];
  }
  const color = searchParams.get("color") || "#0284c7";

  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="60" height="60" viewBox="0 0 60 60">
    <circle cx="30" cy="30" r="30" fill="${color}"/>
    <text x="50%" y="54%" font-family="system-ui, -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, sans-serif" font-weight="800" font-size="24" fill="#ffffff" dominant-baseline="middle" text-anchor="middle">${initial}</text>
  </svg>`.trim();

  return new NextResponse(svg, {
    headers: {
      "Content-Type": "image/svg+xml",
      "Cache-Control": "public, max-age=3600",
    },
  });
}
