import { cookies } from "next/headers";
import { AUTH_COOKIE } from "@/shared/config/constants";
import { env, isMockMode } from "@/shared/config/env";
import { MOCK_USER } from "./fixtures";

export interface InitialUser {
  readonly id: string;
  readonly email: string;
  readonly username: string;
  readonly displayName: string;
  readonly createdAt: string;
}

export async function getInitialUser(): Promise<InitialUser | null> {
  const jar = await cookies();
  const token = jar.get(AUTH_COOKIE)?.value;
  if (!token) return null;

  if (isMockMode()) {
    return MOCK_USER as InitialUser;
  }

  try {
    const res = await fetch(`${env.NEXT_PUBLIC_API_BASE_URL}/api/users/me`, {
      headers: { Authorization: `Bearer ${token}` },
      cache: "no-store",
    });
    if (!res.ok) return null;
    const data = (await res.json()) as { user: InitialUser };
    return data.user;
  } catch {
    return null;
  }
}
