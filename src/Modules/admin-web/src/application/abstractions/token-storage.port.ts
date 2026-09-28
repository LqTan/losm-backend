/**
 * Port: where the token is stored. Extracted so the backend can later move
 * from localStorage to an httpOnly cookie without touching the application layer.
 */
export interface TokenStorage {
  get(): string | null;
  save(token: string): void;
  clear(): void;
}

export type UnauthorizedListener = () => void;

/**
 * Port: notified when the server rejects the token (401/403).
 * Infrastructure invokes this callback; presentation decides how to react
 * (clear the session, redirect to the login page).
 */
export interface UnauthorizedNotifier {
  onUnauthorized(listener: UnauthorizedListener): void;
}
