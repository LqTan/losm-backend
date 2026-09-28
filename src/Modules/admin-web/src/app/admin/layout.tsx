"use client";

import { useEffect, type ReactNode } from "react";
import { useRouter } from "next/navigation";
import { AdminShell } from "@/presentation/components/global";
import { Spinner } from "@/presentation/components/ui";
import { useAuth } from "@/presentation/hooks";

/**
 * Layout for the whole /admin area.
 * Blocks rendering until the session is restored to avoid a content flash.
 */
export default function AdminLayout({ children }: { children: ReactNode }) {
  const router = useRouter();
  const { user, isLoading } = useAuth();

  useEffect(() => {
    if (!isLoading && !user) {
      router.replace("/login");
    }
  }, [isLoading, user, router]);

  if (isLoading || !user) {
    return (
      <div
        className="d-flex align-items-center justify-content-center"
        style={{ minHeight: "100vh" }}
      >
        <Spinner label="Loading admin panel…" />
      </div>
    );
  }

  return <AdminShell>{children}</AdminShell>;
}
