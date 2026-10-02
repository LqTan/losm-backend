import type { NextRequest } from "next/server";
import { NextResponse } from "next/server";
import {
  MOCK_CHAT_SESSIONS,
  MOCK_GOOGLE_CALENDAR_STATUS,
  MOCK_MEETINGS_PAST,
  MOCK_MEETINGS_UPCOMING,
  MOCK_PLACES,
  MOCK_REVIEWS,
  MOCK_SAVED_PLACES,
  MOCK_SESSION_HISTORY,
  MOCK_TOKEN,
  MOCK_USER,
} from "./fixtures";
import type { AttachedPlace } from "@/entities/chat-session/model";
import type { ActionId } from "@/shared/lib/brands";

interface MockResponse {
  readonly status: number;
  readonly body: unknown;
  readonly headers?: Record<string, string>;
}

const json = (status: number, body: unknown): MockResponse => ({ status, body });

function matchExact(tail: string, pattern: string): boolean {
  return tail === pattern;
}

function matchStartsWith(tail: string, prefix: string): boolean {
  return tail === prefix || tail.startsWith(`${prefix}/`);
}

function matchCatchAll(tail: string, prefix: string): string | null {
  if (tail === prefix) return "";
  if (tail.startsWith(`${prefix}/`)) return tail.slice(prefix.length + 1);
  return null;
}

function getQueryParam(url: string, key: string): string | null {
  try {
    const u = new URL(url);
    return u.searchParams.get(key);
  } catch {
    return null;
  }
}

async function readJsonBody(req: NextRequest): Promise<Record<string, unknown>> {
  try {
    return (await req.json()) as Record<string, unknown>;
  } catch {
    return {};
  }
}

export async function tryHandleMock(req: NextRequest, tail: string): Promise<Response | null> {
  const method = req.method.toUpperCase();
  const url = req.nextUrl.toString();
  const body = method === "GET" || method === "HEAD" ? {} : await readJsonBody(req);

  if (matchExact(tail, "users/login") && method === "POST") {
    return respond(json(200, { ...MOCK_USER, token: MOCK_TOKEN }));
  }
  if (matchExact(tail, "users/register") && method === "POST") {
    return respond(json(200, { ...MOCK_USER, token: MOCK_TOKEN }));
  }
  if (matchExact(tail, "users/me") && method === "GET") {
    return respond(json(200, { user: MOCK_USER }));
  }
  if (matchExact(tail, "users/me") && method === "PUT") {
    const username = String(body.username ?? MOCK_USER.username);
    return respond(json(200, { ...MOCK_USER, username }));
  }
  if (matchStartsWith(tail, "users/me/reviews") && method === "GET") {
    return respond(json(200, MOCK_REVIEWS));
  }
  if (matchExact(tail, "users/me/meetings") && method === "GET") {
    const status = getQueryParam(url, "status");
    if (status === "past") return respond(json(200, MOCK_MEETINGS_PAST));
    return respond(json(200, MOCK_MEETINGS_UPCOMING));
  }
  if (matchExact(tail, "users/me/google-calendar/status") && method === "GET") {
    return respond(json(200, MOCK_GOOGLE_CALENDAR_STATUS));
  }
  if (matchExact(tail, "users/me/google-calendar/disconnect") && method === "DELETE") {
    return new Response(null, { status: 204 });
  }
  if (matchExact(tail, "users/me/google-calendar") && method === "DELETE") {
    return new Response(null, { status: 204 });
  }
  if (matchStartsWith(tail, "users/me/google-calendar/connect") && method === "GET") {
    return NextResponse.redirect(
      new URL("/profile?gcal=connected", req.url),
      { status: 302 },
    );
  }
  if (matchExact(tail, "users/me/google-calendar/callback") && method === "GET") {
    return NextResponse.redirect(
      new URL("/profile?gcal=connected", req.url),
      { status: 302 },
    );
  }

  if (matchExact(tail, "saved-places") && method === "GET") {
    return respond(json(200, MOCK_SAVED_PLACES));
  }
  if (matchExact(tail, "saved-places") && method === "POST") {
    return new Response(null, { status: 204 });
  }
  const unsaveId = matchCatchAll(tail, "saved-places");
  if (unsaveId !== null && method === "DELETE") {
    return new Response(null, { status: 204 });
  }

  if (matchExact(tail, "reviews") && method === "POST") {
    const placeId = String(body.placeId ?? "");
    const place = MOCK_PLACES.find((p) => p.id === placeId);
    return respond(
      json(200, {
        id: `r-${Date.now()}`,
        placeId,
        placeName: place?.name ?? "Unknown",
        rating: Number(body.rating ?? 5),
        comment: String(body.comment ?? ""),
        createdAt: new Date().toISOString(),
      }),
    );
  }
  const reviewPlaceId = matchCatchAll(tail, "reviews/place");
  if (reviewPlaceId !== null && method === "GET") {
    const [placeId, sub] = reviewPlaceId.split("/");
    if (sub === "average-rating") {
      const ratings = MOCK_REVIEWS.filter((r) => r.placeId === placeId).map(
        (r) => r.rating,
      );
      const avg =
        ratings.length > 0
          ? ratings.reduce((a, b) => a + b, 0) / ratings.length
          : 4.3;
      return respond(json(200, { average: avg }));
    }
    const filtered = MOCK_REVIEWS.filter((r) => r.placeId === placeId);
    return respond(json(200, filtered));
  }

  if (matchExact(tail, "search") && method === "GET") {
    const q = (getQueryParam(url, "query") ?? "").toLowerCase();
    const filtered =
      q.length === 0
        ? MOCK_PLACES
        : MOCK_PLACES.filter((p) =>
            (p.name + " " + p.address + " " + p.categories.join(" "))
              .toLowerCase()
              .includes(q),
          );
    return respond(json(200, filtered));
  }
  if (matchExact(tail, "search/meeting-places") && method === "POST") {
    const q = String(body.query ?? "").toLowerCase();
    const filtered =
      q.length === 0
        ? MOCK_PLACES.slice(0, 5)
        : MOCK_PLACES.filter((p) =>
            (p.name + " " + p.categories.join(" "))
              .toLowerCase()
              .includes(q),
          ).slice(0, 5);
    return respond(json(200, filtered));
  }

  const placeId = matchCatchAll(tail, "places");
  if (placeId !== null && method === "GET") {
    const place = MOCK_PLACES.find((p) => p.id === placeId);
    if (!place) return respond(json(404, { error: "not_found" }));
    return respond(json(200, place));
  }

  if (matchExact(tail, "agent/sessions") && method === "GET") {
    return respond(json(200, MOCK_CHAT_SESSIONS));
  }
  const sessionId = matchCatchAll(tail, "agent/sessions");
  if (sessionId !== null && method === "GET") {
    const history = { ...MOCK_SESSION_HISTORY, sessionId: sessionId as never };
    return respond(json(200, history));
  }
  const pendingId = matchCatchAll(tail, "agent/pending-actions");
  if (pendingId !== null && method === "GET") {
    return respond(
      json(200, {
        actionId: pendingId as ActionId,
        description: "Tạo cuộc hẹn với khách hàng",
        status: "Pending",
        payload: {
          title: "Họp khách A",
          startAt: new Date(Date.now() + 86400_000).toISOString(),
          durationMinutes: 60,
          attendees: ["a@example.com"],
          note: "",
          placeName: "Highlands Coffee Vincom",
          placeId: "p-001",
        },
      }),
    );
  }
  if (matchExact(tail, "agent/confirm") && method === "POST") {
    return respond(json(200, { actionId: body.actionId ?? "a-001" }));
  }
  if (matchExact(tail, "agent/retry-emails") && method === "POST") {
    return respond(json(200, { actionId: body.originalActionId ?? "a-001" }));
  }
  if (matchExact(tail, "agent/stream") && method === "POST") {
    return respondStream(req, body);
  }

  return null;
}

function respond(res: MockResponse): Response {
  return NextResponse.json(res.body, { status: res.status });
}

function respondStream(req: NextRequest, body: Record<string, unknown>): Response {
  const encoder = new TextEncoder();
  const message = String(body.message ?? "");
  const lower = message.toLowerCase();
  const wantsMeeting =
    lower.includes("đặt lịch") ||
    lower.includes("book") ||
    lower.includes("lịch hẹn");
  const sessionId = (body.sessionId as string | undefined) ?? "s-001";

  const stream = new ReadableStream({
    async start(controller) {
      const steps = wantsMeeting
        ? [
            { delay: 100, kind: "ToolCall", tool: "geocode_place", summary: "Đang gọi geocode_place…" },
            { delay: 400, kind: "ToolResult", tool: "geocode_place", summary: "Đã nhận kết quả từ geocode_place." },
            { delay: 700, kind: "ToolCall", tool: "search_places", summary: "Đang gọi search_places…" },
            { delay: 1100, kind: "ToolResult", tool: "search_places", summary: "Đã nhận kết quả từ search_places." },
            { delay: 1400, kind: "PendingAction", tool: "create_meeting", summary: "Đang chuẩn bị hành động cần xác nhận…" },
          ]
        : [
            { delay: 100, kind: "ToolCall", tool: "geocode_place", summary: "Đang gọi geocode_place…" },
            { delay: 400, kind: "ToolResult", tool: "geocode_place", summary: "Đã nhận kết quả từ geocode_place." },
            { delay: 700, kind: "ToolCall", tool: "search_places", summary: "Đang gọi search_places…" },
            { delay: 1100, kind: "ToolResult", tool: "search_places", summary: "Đã nhận kết quả từ search_places." },
            { delay: 1400, kind: "Finalize", tool: null, summary: "Đang soạn câu trả lời…" },
          ];

      let order = 1;
      for (const s of steps) {
        await sleep(s.delay);
        const evt = {
          type: "step",
          order: order++,
          kind: s.kind,
          toolName: s.tool,
          summary: s.summary,
          succeeded: true,
        };
        controller.enqueue(encoder.encode(JSON.stringify(evt) + "\n"));
      }

      const places: readonly AttachedPlace[] = MOCK_PLACES.slice(0, 5).map((p) => ({
        id: p.id,
        name: p.name,
        address: p.address,
        location: p.location,
        category: p.categories[0] ?? null,
        openingHours: "07:00 - 22:00",
        distanceKm: p.distanceKm,
        finalScore: p.finalScore ?? null,
      }));

      const result = {
        type: "result",
        sessionId,
        answer: wantsMeeting
          ? `Tôi đã tìm thấy 5 địa điểm phù hợp. Bạn muốn đặt lịch tại địa điểm nào? Tôi đã chuẩn bị một hành động cần xác nhận.`
          : `Dưới đây là 5 địa điểm phù hợp với yêu cầu của bạn, được sắp xếp theo điểm phù hợp và khoảng cách.`,
        places,
        pendingActionIds: wantsMeeting ? ["a-mock-001"] : [],
      };
      controller.enqueue(encoder.encode(JSON.stringify(result) + "\n"));
      controller.close();
    },
  });

  return new Response(stream, {
    status: 200,
    headers: {
      "Content-Type": "application/x-ndjson",
      "Cache-Control": "no-store",
    },
  });
}

function sleep(ms: number): Promise<void> {
  return new Promise((r) => setTimeout(r, ms));
}
