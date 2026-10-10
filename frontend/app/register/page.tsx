"use client";

import { Suspense, useState, useEffect } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { setAuth, LoginResponse } from "@/lib/auth";
import { api, ApiError } from "@/lib/api";
import { ArrowLeft, CheckCircle2, Loader2, UserPlus } from "lucide-react";

function RegisterContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const emailParam = searchParams.get("email") || "";
  const redirectParam = searchParams.get("redirect") || "/profile/calendar";

  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (emailParam) {
      setEmail(emailParam);
      const prefix = emailParam.split("@")[0];
      if (prefix) {
        setUsername(prefix.charAt(0).toUpperCase() + prefix.slice(1));
      }
    }
  }, [emailParam]);

  // Điều hướng trực tiếp sang Google OAuth
  function handleGoogleLogin() {
    const apiBase = process.env.NEXT_PUBLIC_API_BASE_URL || "http://localhost:5232";
    window.location.href = `${apiBase}/api/users/google-login?redirect=${encodeURIComponent(redirectParam)}`;
  }

  // Đăng ký thông thường
  async function handleRegister(e: React.FormEvent) {
    e.preventDefault();
    setError(null);

    if (password.length < 6) {
      setError("Mật khẩu phải chứa ít nhất 6 ký tự.");
      return;
    }

    if (password !== confirmPassword) {
      setError("Mật khẩu xác nhận không khớp.");
      return;
    }

    setLoading(true);

    try {
      await api.post<unknown>("/api/users/register", {
        username: username.trim(),
        email: email.trim().toLowerCase(),
        password,
      });

      const loginRes = await api.post<unknown>("/api/users/login", {
        email: email.trim().toLowerCase(),
        password,
      });

      const parsed = LoginResponse.parse(loginRes);
      setAuth(parsed.token, parsed.role, parsed);

      router.replace(redirectParam);
    } catch (err) {
      if (err instanceof ApiError) {
        setError(err.message);
      } else {
        setError("Đăng ký không thành công. Vui lòng kiểm tra lại thông tin.");
      }
    } finally {
      setLoading(false);
    }
  }

  const isInvite = redirectParam.includes("calendar");

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/20 p-4">
      <div className="w-full max-w-sm space-y-4">
        <Link
          href="/"
          className="inline-flex items-center gap-1.5 text-xs text-muted-foreground hover:text-foreground transition"
        >
          <ArrowLeft className="h-3.5 w-3.5" />
          <span>Quay lại Bản đồ</span>
        </Link>

        <Card className="shadow-lg border bg-card">
          <CardHeader className="space-y-1.5 pb-4">
            <CardTitle className="text-xl font-bold flex items-center gap-2">
              <UserPlus className="h-5 w-5 text-muted-foreground" />
              <span>Tạo tài khoản</span>
            </CardTitle>
            <CardDescription className="text-xs">
              {isInvite
                ? "Tài khoản của bạn chưa có trên LOSM. Vui lòng đăng ký nhanh để vào xem lịch hẹn."
                : "Đăng ký tài khoản LOSM để khám phá và tạo điểm hẹn cùng bạn bè."}
            </CardDescription>
          </CardHeader>

          <CardContent className="space-y-4">
            {/* Nút đăng ký Google chuẩn redirect */}
            <div className="space-y-2">
              <Button
                type="button"
                variant="outline"
                className="w-full h-9 text-xs font-medium flex items-center justify-center gap-2 border bg-card hover:bg-muted cursor-pointer"
                onClick={handleGoogleLogin}
              >
                <svg className="h-4 w-4" viewBox="0 0 24 24">
                  <path
                    fill="#4285F4"
                    d="M22.56 12.25c0-.78-.07-1.53-.2-2.25H12v4.26h5.92c-.26 1.37-1.04 2.53-2.21 3.31v2.77h3.57c2.08-1.92 3.28-4.74 3.28-8.09z"
                  />
                  <path
                    fill="#34A853"
                    d="M12 23c2.97 0 5.46-.98 7.28-2.66l-3.57-2.77c-.98.66-2.23 1.06-3.71 1.06-2.86 0-5.29-1.93-6.16-4.53H2.18v2.84C3.99 20.53 7.7 23 12 23z"
                  />
                  <path
                    fill="#FBBC05"
                    d="M5.84 14.09c-.22-.66-.35-1.36-.35-2.09s.13-1.43.35-2.09V7.06H2.18C1.43 8.55 1 10.22 1 12s.43 3.45 1.18 4.94l2.85-2.22.81-.63z"
                  />
                  <path
                    fill="#EA4335"
                    d="M12 5.38c1.62 0 3.06.56 4.21 1.64l3.15-3.15C17.45 2.09 14.97 1 12 1 7.7 1 3.99 3.47 2.18 7.06l3.66 2.84c.87-2.6 3.3-4.52 6.16-4.52z"
                  />
                </svg>
                <span>Đăng ký bằng tài khoản Google</span>
              </Button>

              <div className="relative flex items-center justify-center my-3">
                <div className="border-t border-border w-full" />
                <span className="bg-card px-2 text-[10px] uppercase text-muted-foreground tracking-wider absolute">
                  hoặc đăng ký bằng email
                </span>
              </div>
            </div>

            <form onSubmit={handleRegister} className="space-y-3">
              <div className="space-y-1">
                <Label htmlFor="username" className="text-xs font-medium">
                  Tên hiển thị
                </Label>
                <Input
                  id="username"
                  type="text"
                  placeholder="Ví dụ: Hoàng Nam"
                  value={username}
                  onChange={(e) => setUsername(e.target.value)}
                  className="h-9 text-sm"
                  required
                />
              </div>

              <div className="space-y-1">
                <Label htmlFor="email" className="text-xs font-medium">
                  Địa chỉ Email
                </Label>
                <Input
                  id="email"
                  type="email"
                  placeholder="name@example.com"
                  value={email}
                  onChange={(e) => setEmail(e.target.value)}
                  className="h-9 text-sm"
                  required
                />
              </div>

              <div className="space-y-1">
                <Label htmlFor="password" className="text-xs font-medium">
                  Mật khẩu
                </Label>
                <Input
                  id="password"
                  type="password"
                  placeholder="Tối thiểu 6 ký tự"
                  value={password}
                  onChange={(e) => setPassword(e.target.value)}
                  className="h-9 text-sm"
                  required
                />
              </div>

              <div className="space-y-1">
                <Label htmlFor="confirmPassword" className="text-xs font-medium">
                  Xác nhận mật khẩu
                </Label>
                <Input
                  id="confirmPassword"
                  type="password"
                  placeholder="Nhập lại mật khẩu"
                  value={confirmPassword}
                  onChange={(e) => setConfirmPassword(e.target.value)}
                  className="h-9 text-sm"
                  required
                />
              </div>

              {error && (
                <div className="rounded-lg bg-red-50 dark:bg-red-950/30 border border-red-200 dark:border-red-900/50 p-2.5 text-xs text-red-600 dark:text-red-400">
                  {error}
                </div>
              )}

              <Button
                type="submit"
                className="w-full h-9 text-xs font-medium mt-1"
                disabled={loading}
              >
                {loading ? (
                  <>
                    <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                    <span>Đang tạo tài khoản...</span>
                  </>
                ) : (
                  <span className="flex items-center gap-1.5">
                    <CheckCircle2 className="h-3.5 w-3.5" />
                    Đăng ký & Xem lịch hẹn
                  </span>
                )}
              </Button>
            </form>

            <div className="pt-2 text-center text-xs text-muted-foreground border-t">
              Đã có tài khoản?{" "}
              <Link
                href={`/login?${searchParams.toString()}`}
                className="font-semibold text-foreground underline underline-offset-4 hover:opacity-80 transition"
              >
                Đăng nhập ngay
              </Link>
            </div>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}

export default function RegisterPage() {
  return (
    <Suspense
      fallback={
        <div className="flex min-h-screen items-center justify-center bg-muted/20">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      }
    >
      <RegisterContent />
    </Suspense>
  );
}
