"use client";

import { Suspense, useEffect } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { setAuth } from "@/lib/auth";
import { Loader2 } from "lucide-react";

function AuthCallbackContent() {
  const router = useRouter();
  const searchParams = useSearchParams();

  useEffect(() => {
    const token = searchParams.get("token");
    const username = searchParams.get("username");
    const email = searchParams.get("email");
    const role = searchParams.get("role") || "User";
    const redirect = searchParams.get("redirect") || "/profile/calendar";

    if (token && username && email) {
      setAuth(token, role, {
        id: "",
        username,
        email,
      });
      router.replace(redirect);
    } else {
      router.replace("/login?error=auth_failed");
    }
  }, [searchParams, router]);

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/20">
      <div className="flex flex-col items-center gap-3 text-center">
        <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        <p className="text-sm font-medium text-foreground">
          Đang đồng bộ tài khoản Google của bạn...
        </p>
      </div>
    </div>
  );
}

export default function AuthCallbackPage() {
  return (
    <Suspense
      fallback={
        <div className="flex min-h-screen items-center justify-center bg-muted/20">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      }
    >
      <AuthCallbackContent />
    </Suspense>
  );
}
