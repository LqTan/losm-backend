"use server";

import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AUTH_COOKIE } from "@/shared/config/constants";
import { register as registerApi } from "@/entities/user";

export interface RegisterResult {
  readonly ok: boolean;
  readonly error?: string;
}

export async function registerAction(formData: FormData): Promise<RegisterResult> {
  const email = String(formData.get("email") ?? "").trim();
  const password = String(formData.get("password") ?? "");
  const username = String(formData.get("username") ?? "").trim();
  const displayName = String(formData.get("displayName") ?? "").trim();
  if (!email || !password || !username) {
    return { ok: false, error: "Vui lòng nhập đầy đủ thông tin." };
  }
  try {
    const res = await registerApi({ email, password, username, displayName });
    const jar = await cookies();
    jar.set(AUTH_COOKIE, res.token, {
      path: "/",
      httpOnly: true,
      sameSite: "lax",
      secure: process.env.NODE_ENV === "production",
      maxAge: 60 * 60 * 24 * 7,
    });
  } catch {
    return { ok: false, error: "Không tạo được tài khoản. Email có thể đã tồn tại." };
  }
  redirect("/");
}
