"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { Bot } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { InlineError } from "@/shared/ui/inline-error";
import { COPY } from "@/shared/config/copy";

export function RegisterView() {
  const router = useRouter();
  const [error, setError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  function onSubmit(formData: FormData) {
    setError(null);
    startTransition(async () => {
      const email = String(formData.get("email") ?? "").trim();
      const password = String(formData.get("password") ?? "");
      const username = String(formData.get("username") ?? "").trim();
      const displayName = String(formData.get("displayName") ?? "").trim();
      try {
        const res = await fetch("/api/proxy/users/register", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ email, password, username, displayName }),
        });
        if (!res.ok) {
          setError("Không tạo được tài khoản. Email có thể đã tồn tại.");
          return;
        }
        const data = (await res.json()) as { token: string };
        document.cookie = `scalar_token=${encodeURIComponent(
          data.token,
        )}; Path=/; Max-Age=${60 * 60 * 24 * 7}; SameSite=Lax`;
        router.replace("/");
        router.refresh();
      } catch {
        setError(COPY.errors.network);
      }
    });
  }

  return (
    <div className="flex min-h-dvh items-center justify-center bg-muted/30 p-4">
      <Card className="w-full max-w-sm">
        <CardHeader>
          <div className="flex items-center gap-2">
            <Bot size={20} strokeWidth={1.5} aria-hidden />
            <CardTitle>{COPY.auth.registerTitle}</CardTitle>
          </div>
          <CardDescription>{COPY.app.tagline}</CardDescription>
        </CardHeader>
        <CardContent>
          <form action={onSubmit} className="flex flex-col gap-3">
            <div className="flex flex-col gap-1">
              <Label htmlFor="email">{COPY.auth.email}</Label>
              <Input id="email" name="email" type="email" autoComplete="email" required />
            </div>
            <div className="flex flex-col gap-1">
              <Label htmlFor="username">Username</Label>
              <Input id="username" name="username" autoComplete="username" required />
            </div>
            <div className="flex flex-col gap-1">
              <Label htmlFor="displayName">{COPY.auth.displayName}</Label>
              <Input id="displayName" name="displayName" autoComplete="nickname" />
            </div>
            <div className="flex flex-col gap-1">
              <Label htmlFor="password">{COPY.auth.password}</Label>
              <Input
                id="password"
                name="password"
                type="password"
                autoComplete="new-password"
                required
                minLength={6}
              />
            </div>
            {error ? <InlineError message={error} /> : null}
            <Button type="submit" disabled={pending}>
              {pending ? "Đang tạo tài khoản…" : COPY.auth.submitRegister}
            </Button>
            <p className="text-center text-[13px] text-muted-foreground">
              <Link href="/login" className="hover:underline">
                {COPY.auth.switchToLogin}
              </Link>
            </p>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
