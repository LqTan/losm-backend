export function getApiBase() {
  if (typeof window !== "undefined") {
    return (
      process.env.NEXT_PUBLIC_API_BASE ?? "http://localhost:5232"
    );
  }
  return process.env.NEXT_PUBLIC_API_BASE ?? "http://localhost:5232";
}

export type Json = Record<string, unknown>;

export class ApiError extends Error {
  status: number;
  body: unknown;
  constructor(status: number, message: string, body?: unknown) {
    super(message);
    this.status = status;
    this.body = body;
  }
}

async function request<T>(
  path: string,
  init: RequestInit & { token?: string | null } = {},
): Promise<T> {
  const headers = new Headers(init.headers);
  if (!headers.has("Content-Type") && init.body) {
    headers.set("Content-Type", "application/json");
  }
  if (init.token) {
    headers.set("Authorization", `Bearer ${init.token}`);
  }

  const url = `${getApiBase()}${path}`;
  const res = await fetch(url, { ...init, headers });

  const ct = res.headers.get("content-type") ?? "";
  const data: unknown = ct.includes("application/json")
    ? await res.json().catch(() => null)
    : await res.text().catch(() => null);

  if (!res.ok) {
    const message =
      data && typeof data === "object" && "message" in data
        ? String((data as { message?: unknown }).message ?? res.statusText)
        : res.statusText;
    throw new ApiError(res.status, message, data);
  }

  return data as T;
}

export const api = {
  get: <T>(path: string, token?: string | null) =>
    request<T>(path, { method: "GET", token }),
  post: <T>(path: string, body: unknown, token?: string | null) =>
    request<T>(path, { method: "POST", body: JSON.stringify(body), token }),
  put: <T>(path: string, body: unknown, token?: string | null) =>
    request<T>(path, { method: "PUT", body: JSON.stringify(body), token }),
  del: <T>(path: string, token?: string | null) =>
    request<T>(path, { method: "DELETE", token }),
};