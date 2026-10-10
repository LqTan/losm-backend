"use client";

import { Suspense, useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { getUser } from "@/lib/auth";
import { api } from "@/lib/api";
import { Calendar, Loader2 } from "lucide-react";

function MeetingViewContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const emailParam = searchParams.get("email");
  const [, setChecking] = useState(true);

  useEffect(() => {
    async function checkAndRedirect() {
      // 1. Nếu trình duyệt đang có session đăng nhập, vào thẳng Calendar
      const currentUser = getUser();
      if (currentUser) {
        router.replace("/profile/calendar");
        return;
      }

      // 2. Nếu không có param email, đưa về trang đăng nhập
      if (!emailParam) {
        router.replace("/login?redirect=/profile/calendar");
        return;
      }

      const email = emailParam.trim().toLowerCase();

      try {
        // 3. Kiểm tra xem email đã có tài khoản trên hệ thống chưa
        const res = await api.get<{ exists: boolean; email: string }>(
          `/api/users/check-email?email=${encodeURIComponent(email)}`
        );

        if (res.exists) {
          // Đã có tài khoản: chuyển sang trang login với email điền sẵn
          router.replace(
            `/login?email=${encodeURIComponent(email)}&redirect=/profile/calendar`
          );
        } else {
          // Chưa có tài khoản: chuyển sang trang đăng ký
          router.replace(
            `/register?email=${encodeURIComponent(email)}&redirect=/profile/calendar`
          );
        }
      } catch {
        // Fallback: chuyển sang login nếu có lỗi
        router.replace(
          `/login?email=${encodeURIComponent(email)}&redirect=/profile/calendar`
        );
      } finally {
        setChecking(false);
      }
    }

    checkAndRedirect();
  }, [emailParam, router]);

  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/20 p-4">
      <div className="w-full max-w-sm rounded-2xl border bg-card p-6 shadow-xl text-center space-y-4">
        <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-2xl bg-primary/10 text-primary">
          <Calendar className="h-6 w-6" />
        </div>
        <div className="space-y-1">
          <h2 className="text-base font-semibold text-foreground">
            Đang mở chi tiết lịch hẹn
          </h2>
          <p className="text-xs text-muted-foreground">
            Hệ thống đang kiểm tra phiên làm việc của bạn, vui lòng đợi trong giây lát...
          </p>
        </div>
        <div className="flex items-center justify-center pt-2">
          <Loader2 className="h-5 w-5 animate-spin text-muted-foreground" />
        </div>
      </div>
    </div>
  );
}

export default function MeetingViewPage() {
  return (
    <Suspense
      fallback={
        <div className="flex min-h-screen items-center justify-center bg-muted/20">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      }
    >
      <MeetingViewContent />
    </Suspense>
  );
}
