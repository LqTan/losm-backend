import { z } from "zod";
import { toast } from "sonner";

const TokenKey = "losm_token";
const RoleKey = "losm_role";
const UserKey = "losm_user";

export const AUTH_CHANGE_EVENT = "losm:auth-change";

export interface StoredUser {
  id: string;
  username: string;
  email: string;
  role: string;
  avatarUrl?: string;
}

function parseJwt(token: string): any {
  try {
    const base64Url = token.split(".")[1];
    if (!base64Url) return null;
    const base64 = base64Url.replace(/-/g, "+").replace(/_/g, "/");
    const jsonPayload = decodeURIComponent(
      atob(base64)
        .split("")
        .map((c) => "%" + ("00" + c.charCodeAt(0).toString(16)).slice(-2))
        .join("")
    );
    return JSON.parse(jsonPayload);
  } catch {
    return null;
  }
}

export function isTokenExpired(token: string): boolean {
  if (!token) return true;
  const payload = parseJwt(token);
  if (!payload || typeof payload.exp !== "number") return false;
  return Date.now() >= payload.exp * 1000;
}

export function notifyAuthChange() {
  if (typeof window !== "undefined") {
    window.dispatchEvent(new CustomEvent(AUTH_CHANGE_EVENT));
  }
}

let lastToastTime = 0;

export function handleAuthExpired(customMessage?: string) {
  if (typeof window === "undefined") return;
  const hadToken = !!localStorage.getItem(TokenKey);
  clearAuth();
  const now = Date.now();
  if (hadToken && now - lastToastTime > 3000) {
    lastToastTime = now;
    toast.error(
      customMessage || "Phiên đăng nhập đã hết hạn. Vui lòng đăng nhập lại."
    );
  }
}

export function setAuth(
  token: string,
  role: string,
  user?: { id: string; username: string; email: string; avatarUrl?: string }
) {
  if (typeof window === "undefined") return;
  localStorage.setItem(TokenKey, token);
  localStorage.setItem(RoleKey, role);
  if (user) {
    localStorage.setItem(UserKey, JSON.stringify({ ...user, role }));
  }
  document.cookie = `${TokenKey}=${token}; Path=/; SameSite=Lax; Max-Age=86400`;
  notifyAuthChange();
}

export function clearAuth() {
  if (typeof window === "undefined") return;
  localStorage.removeItem(TokenKey);
  localStorage.removeItem(RoleKey);
  localStorage.removeItem(UserKey);
  document.cookie = `${TokenKey}=; Path=/; Max-Age=0`;
  notifyAuthChange();
}

export function getToken(): string | null {
  if (typeof window === "undefined") return null;
  const token = localStorage.getItem(TokenKey);
  if (!token) return null;
  if (isTokenExpired(token)) {
    handleAuthExpired();
    return null;
  }
  return token;
}

export function getRole(): string | null {
  if (typeof window === "undefined") return null;
  const token = localStorage.getItem(TokenKey);
  if (!token || isTokenExpired(token)) return null;
  return localStorage.getItem(RoleKey);
}

export function getUser(): StoredUser | null {
  if (typeof window === "undefined") return null;
  const token = localStorage.getItem(TokenKey);
  if (token && isTokenExpired(token)) {
    handleAuthExpired();
    return null;
  }
  const raw = localStorage.getItem(UserKey);
  if (!raw) return null;
  try {
    return JSON.parse(raw) as StoredUser;
  } catch {
    return null;
  }
}

export function isAdmin(): boolean {
  return getRole() === "Admin";
}

export const LoginResponse = z.object({
  id: z.string(),
  username: z.string(),
  email: z.string(),
  token: z.string(),
  role: z.string(),
});