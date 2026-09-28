import type {
  UnauthorizedNotifier,
  TokenStorage,
  UnauthorizedListener,
} from "@/application/abstractions/token-storage.port";

export const TOKEN_STORAGE_KEY = "losm.admin.token";
export const TOKEN_COOKIE_NAME = "losm_admin_token";

/**
 * Stores the token in localStorage and mirrors it into a cookie.
 *
 * The cookie mirror exists because src/proxy.ts runs on the Edge runtime and
 * cannot read localStorage — without it the proxy cannot redirect. The real
 * token is still read from localStorage; the cookie only answers "is there a token?".
 */
export class LocalTokenStorage implements TokenStorage {
  get(): string | null {
    if (typeof window === "undefined") return null;
    return window.localStorage.getItem(TOKEN_STORAGE_KEY);
  }

  save(token: string): void {
    if (typeof window === "undefined") return;
    window.localStorage.setItem(TOKEN_STORAGE_KEY, token);
    document.cookie = `${TOKEN_COOKIE_NAME}=${encodeURIComponent(
      token,
    )}; path=/; SameSite=Lax`;
  }

  clear(): void {
    if (typeof window === "undefined") return;
    window.localStorage.removeItem(TOKEN_STORAGE_KEY);
    document.cookie = `${TOKEN_COOKIE_NAME}=; path=/; Max-Age=0; SameSite=Lax`;
  }
}

/** Holder for the unauthorized listener — a singleton, the way DI singletons work. */
export class LocalUnauthorizedNotifier implements UnauthorizedNotifier {
  private listener: UnauthorizedListener | null = null;

  onUnauthorized(listener: UnauthorizedListener): void {
    this.listener = listener;
  }

  notify(): void {
    this.listener?.();
  }
}
