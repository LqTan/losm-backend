import type { ReactNode } from "react";
import { cookies } from "next/headers";
import { redirect } from "next/navigation";
import { AUTH_COOKIE } from "@/shared/config/constants";

export default async function MainLayout({ children }: { children: ReactNode }) {
  const jar = await cookies();
  const token = jar.get(AUTH_COOKIE)?.value;
  if (!token) {
    redirect("/login");
  }
  return <>{children}</>;
}
