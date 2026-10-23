export function getApiBase() {
  const envBase =
    process.env.NEXT_PUBLIC_API_BASE || process.env.NEXT_PUBLIC_API_BASE_URL;
  if (envBase) {
    return envBase;
  }
  if (typeof window !== "undefined") {
    return window.location.origin;
  }
  return "http://localhost:5232";
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

import { handleAuthExpired } from "./auth";
import { getApiErrorMessage } from "./api-error";

async function request<T>(
  path: string,
  init: RequestInit & { token?: string | null } = {},
): Promise<T> {
  const headers = new Headers(init.headers);
  if (!headers.has("Content-Type") && init.body && !(init.body instanceof FormData)) {
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
    if (
      res.status === 401 &&
      !path.includes("/users/login") &&
      !path.includes("/users/register")
    ) {
      handleAuthExpired();
    }
    const message = getApiErrorMessage(data, res.statusText);
    throw new ApiError(res.status, message, data);
  }

  return data as T;
}

export const api = {
  upload: <T>(path: string, body: FormData, token?: string | null) =>
    request<T>(path, { method: "POST", body, token }),
  get: <T>(path: string, token?: string | null) =>
    request<T>(path, { method: "GET", token }),
  post: <T>(path: string, body: unknown, token?: string | null) =>
    request<T>(path, { method: "POST", body: JSON.stringify(body), token }),
  put: <T>(path: string, body: unknown, token?: string | null) =>
    request<T>(path, { method: "PUT", body: JSON.stringify(body), token }),
  del: <T>(path: string, token?: string | null) =>
    request<T>(path, { method: "DELETE", token }),
};