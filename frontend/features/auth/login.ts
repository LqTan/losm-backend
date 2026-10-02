"use server";

import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AUTH_COOKIE } from "@/shared/config/constants";
import { login as loginApi } from "@/entities/user";

export interface LoginResult {
  readonly ok: boolean;
  readonly error?: string;
}

export async function loginAction(formData: FormData): Promise<LoginResult> {
  const email = String(formData.get("email") ?? "").trim();
  const password = String(formData.get("password") ?? "");
  if (!email || !password) {
    return { ok: false, error: "Vui lòng nhập email và mật khẩu." };
  }
  try {
    const res = await loginApi({ email, password });
    const jar = await cookies();
    jar.set(AUTH_COOKIE, res.token, {
      path: "/",
      httpOnly: true,
      sameSite: "lax",
      secure: process.env.NODE_ENV === "production",
      maxAge: 60 * 60 * 24 * 7,
    });
  } catch {
    return { ok: false, error: "Email hoặc mật khẩu không đúng." };
  }
  redirect("/");
}

export async function logoutAction(): Promise<void> {
  const jar = await cookies();
  jar.delete(AUTH_COOKIE);
}
