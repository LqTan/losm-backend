import type { HttpClient, HttpRequest } from "@/application/abstractions/http-client.port";
import type { TokenStorage } from "@/application/abstractions/token-storage.port";
import type { LocalUnauthorizedNotifier } from "@/infrastructure/storage/local-token-storage";
import {
  ConflictError,
  ForbiddenError,
  NetworkError,
  NotFoundError,
  UnauthorizedError,
  ValidationError,
} from "@/domain/errors/app-error";
import { appConfig } from "@/infrastructure/config/env";

interface ApiErrorPayload {
  readonly message?: string;
  readonly title?: string;
}

function buildUrl(path: string, query?: Record<string, unknown>): string {
  const url = new URL(
    path.startsWith("/") ? path : `/${path}`,
    appConfig.apiBaseUrl,
  );

  if (query) {
    for (const [key, raw] of Object.entries(query)) {
      const value = raw as string | number | boolean | null | undefined;
      if (value === undefined || value === null || value === "") continue;
      url.searchParams.set(key, String(value));
    }
  }

  return url.toString();
}

/** Maps an HTTP status to a domain error so upper layers stay HTTP-free. */
function toDomainError(status: number, payload: ApiErrorPayload | null) {
  const message =
    payload?.message ?? payload?.title ?? `Request failed (${status}).`;

  if (status === 400) return new ValidationError(message);
  if (status === 401) return new UnauthorizedError(message);
  if (status === 403) return new ForbiddenError(message);
  if (status === 404) return new NotFoundError(message);
  if (status === 409) return new ConflictError(message);
  return new Error(message);
}

/**
 * HttpClient adapter built on the browser fetch API.
 * Attaches the Authorization header from TokenStorage unless the request is anonymous.
 * When the server answers 401/403 it notifies the UnauthorizedNotifier so
 * the upper layers can react (clear the session, return to the login page).
 */
export class FetchHttpClient implements HttpClient {
  constructor(
    private readonly tokenStorage: TokenStorage,
    private readonly unauthorizedNotifier?: LocalUnauthorizedNotifier,
  ) {}

  async send<T>(request: HttpRequest) {
    const token = request.anonymous ? null : this.tokenStorage.get();

    const headers: Record<string, string> = {
      Accept: "application/json",
    };
    if (request.body !== undefined) {
      headers["Content-Type"] = "application/json";
    }
    if (token) {
      headers.Authorization = `Bearer ${token}`;
    }

    let response: globalThis.Response;
    try {
      response = await fetch(buildUrl(request.path, request.query), {
        method: request.method,
        headers,
        body:
          request.body !== undefined
            ? JSON.stringify(request.body)
            : undefined,
      });
    } catch {
      throw new NetworkError(
        `Cannot reach the API at ${appConfig.apiBaseUrl}.`,
      );
    }

    if (response.status === 204) {
      return { status: response.status, body: undefined as T };
    }

    const raw = await response.text();
    const payload = raw ? (JSON.parse(raw) as T & ApiErrorPayload) : null;

    if (!response.ok) {
      if (response.status === 401 || response.status === 403) {
        this.unauthorizedNotifier?.notify();
      }
      throw toDomainError(
        response.status,
        payload as ApiErrorPayload | null,
      );
    }

    return { status: response.status, body: payload as T };
  }
}
