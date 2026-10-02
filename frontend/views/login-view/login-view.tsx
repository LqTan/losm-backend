"use client";

import { useState, useTransition } from "react";
import { useRouter } from "next/navigation";
import Link from "next/link";
import { Bot, Eye, EyeOff } from "lucide-react";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Button } from "@/components/ui/button";
import { InlineError } from "@/shared/ui/inline-error";
import { COPY } from "@/shared/config/copy";

export function LoginView() {
  const router = useRouter();
  const [show, setShow] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [pending, startTransition] = useTransition();

  function onSubmit(formData: FormData) {
    setError(null);
    startTransition(async () => {
      const email = String(formData.get("email") ?? "").trim();
      const password = String(formData.get("password") ?? "");
      try {
        const res = await fetch("/api/proxy/users/login", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ email, password }),
        });
        if (!res.ok) {
          setError(COPY.auth.invalidCredentials);
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
            <CardTitle>{COPY.auth.loginTitle}</CardTitle>
          </div>
          <CardDescription>{COPY.app.tagline}</CardDescription>
        </CardHeader>
        <CardContent>
          <form action={onSubmit} className="flex flex-col gap-3">
            <div className="flex flex-col gap-1">
              <Label htmlFor="email">{COPY.auth.email}</Label>
              <Input
                id="email"
                name="email"
                type="email"
                autoComplete="email"
                required
                placeholder="a@example.com"
              />
            </div>
            <div className="flex flex-col gap-1">
              <Label htmlFor="password">{COPY.auth.password}</Label>
              <div className="relative">
                <Input
                  id="password"
                  name="password"
                  type={show ? "text" : "password"}
                  autoComplete="current-password"
                  required
                  className="pr-10"
                />
                <Button
                  type="button"
                  variant="ghost"
                  size="icon-sm"
                  className="absolute right-1 top-1/2 -translate-y-1/2"
                  aria-label={show ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                  onClick={() => setShow((s) => !s)}
                >
                  {show ? <EyeOff size={14} strokeWidth={1.5} /> : <Eye size={14} strokeWidth={1.5} />}
                </Button>
              </div>
            </div>
            {error ? <InlineError message={error} /> : null}
            <Button type="submit" disabled={pending}>
              {pending ? "Đang đăng nhập…" : COPY.auth.submitLogin}
            </Button>
            <p className="text-center text-[13px] text-muted-foreground">
              <Link href="/register" className="hover:underline">
                {COPY.auth.switchToRegister}
              </Link>
            </p>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}
