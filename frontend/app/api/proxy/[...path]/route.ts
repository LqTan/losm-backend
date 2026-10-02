import { cookies } from "next/headers";
import { NextResponse, type NextRequest } from "next/server";
import { env, isMockMode } from "@/shared/config/env";
import { AUTH_COOKIE } from "@/shared/config/constants";
import { tryHandleMock } from "@/shared/mock/handlers";

const HOP_BY_HOP = new Set([
  "connection",
  "keep-alive",
  "proxy-authenticate",
  "proxy-authorization",
  "te",
  "trailers",
  "transfer-encoding",
  "upgrade",
  "host",
  "content-length",
]);

function strip(headers: Headers): Headers {
  const r = new Headers();
  headers.forEach((v, k) => {
    if (!HOP_BY_HOP.has(k.toLowerCase())) r.set(k, v);
  });
  return r;
}

async function handle(req: NextRequest, ctx: RouteContext<"/api/proxy/[...path]">) {
  const { path } = await ctx.params;
  const tail = Array.isArray(path) ? path.join("/") : String(path);

  if (isMockMode()) {
    const mocked = await tryHandleMock(req, tail);
    if (mocked) return mocked;
  }

  const search = req.nextUrl.search;
  const url = `${env.NEXT_PUBLIC_API_BASE_URL}/api/${tail}${search}`;

  const jar = await cookies();
  const token = jar.get(AUTH_COOKIE)?.value ?? "";

  const headers = strip(req.headers);
  headers.set("Accept", "application/json");
  if (token) headers.set("Authorization", `Bearer ${token}`);
  if (req.method !== "GET" && req.method !== "HEAD") {
    const ct = req.headers.get("content-type");
    if (ct) headers.set("Content-Type", ct);
  }

  const init: RequestInit = { method: req.method, headers, cache: "no-store" };
  if (req.method !== "GET" && req.method !== "HEAD") {
    init.body = await req.arrayBuffer();
  }

  let upstream: Response;
  try {
    upstream = await fetch(url, init);
  } catch {
    return NextResponse.json(
      { error: "upstream_unreachable" },
      { status: 502 },
    );
  }

  const out = strip(upstream.headers);
  out.set("Content-Type", upstream.headers.get("Content-Type") ?? "application/json");
  return new Response(upstream.body, { status: upstream.status, headers: out });
}

export const GET = handle;
export const POST = handle;
export const PUT = handle;
export const PATCH = handle;
export const DELETE = handle;
