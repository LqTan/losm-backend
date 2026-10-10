import { z } from "zod";

const TokenKey = "losm_token";
const RoleKey = "losm_role";
const UserKey = "losm_user";

export interface StoredUser {
  id: string;
  username: string;
  email: string;
  role: string;
}

export function setAuth(token: string, role: string, user?: { id: string; username: string; email: string }) {
  if (typeof window === "undefined") return;
  localStorage.setItem(TokenKey, token);
  localStorage.setItem(RoleKey, role);
  if (user) {
    localStorage.setItem(UserKey, JSON.stringify({ ...user, role }));
  }
  document.cookie = `${TokenKey}=${token}; Path=/; SameSite=Lax; Max-Age=86400`;
}

export function clearAuth() {
  if (typeof window === "undefined") return;
  localStorage.removeItem(TokenKey);
  localStorage.removeItem(RoleKey);
  localStorage.removeItem(UserKey);
  document.cookie = `${TokenKey}=; Path=/; Max-Age=0`;
}

export function getToken(): string | null {
  if (typeof window === "undefined") return null;
  return localStorage.getItem(TokenKey);
}

export function getRole(): string | null {
  if (typeof window === "undefined") return null;
  return localStorage.getItem(RoleKey);
}

export function getUser(): StoredUser | null {
  if (typeof window === "undefined") return null;
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