import type { CurrentSessionResponse, LoginResponse, User } from "./model";

async function postJson<T>(url: string, body: unknown): Promise<T> {
  const res = await fetch(url, {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify(body),
  });
  if (!res.ok) {
    throw new Error(`API ${res.status}`);
  }
  return (await res.json()) as T;
}

async function getJson<T>(url: string): Promise<T> {
  const res = await fetch(url, { cache: "no-store" });
  if (!res.ok) throw new Error(`API ${res.status}`);
  return (await res.json()) as T;
}

export async function login(input: {
  email: string;
  password: string;
}): Promise<LoginResponse> {
  return postJson<LoginResponse>("/api/proxy/users/login", input);
}

export async function register(input: {
  email: string;
  password: string;
  username: string;
  displayName: string;
}): Promise<LoginResponse> {
  return postJson<LoginResponse>("/api/proxy/users/register", input);
}

export async function fetchCurrentSession(): Promise<CurrentSessionResponse | null> {
  try {
    return await getJson<CurrentSessionResponse>("/api/proxy/users/me");
  } catch {
    return null;
  }
}

export async function updateProfile(input: {
  username: string;
}): Promise<User> {
  return postJson<User>("/api/proxy/users/me", input);
}
