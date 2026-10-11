"use client";

import { Suspense, useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import Link from "next/link";
import { getToken, getUser } from "@/lib/auth";
import {
  Users,
  Check,
  X,
  Loader2,
  Mail,
  UserPlus,
  LogIn,
  MapPin,
  Sparkles,
  ArrowRight,
  ShieldCheck,
  AlertTriangle,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { toast } from "sonner";

interface InviteInfo {
  id: string;
  senderUsername: string;
  senderEmail: string;
  senderAvatarUrl?: string;
  receiverEmail: string;
  userExists: boolean;
  status: string;
  createdAt: string;
}

function InviteContent() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const token = searchParams.get("token");

  const [isLoading, setIsLoading] = useState(true);
  const [inviteInfo, setInviteInfo] = useState<InviteInfo | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [isResponding, setIsResponding] = useState(false);
  const [resolvedStatus, setResolvedStatus] = useState<"accepted" | "declined" | null>(null);

  const isLoggedIn = !!getToken();
  const currentUser = getUser();

  useEffect(() => {
    if (!token) {
      setError("Mã liên kết lời mời không hợp lệ hoặc bị thiếu.");
      setIsLoading(false);
      return;
    }

    const fetchInviteInfo = async () => {
      try {
        setIsLoading(true);
        const res = await fetch(`/api/friends/invite-info?token=${encodeURIComponent(token)}`);
        const data = await res.json();

        if (!res.ok) {
          throw new Error(data.message || "Không thể tải thông tin lời mời");
        }

        setInviteInfo(data);

        // If not logged in, auto redirect smoothly to register or login after 1s
        if (!getToken()) {
          const redirectPath = `/friends/invite?token=${encodeURIComponent(token)}`;
          if (data.userExists) {
            router.push(`/login?email=${encodeURIComponent(data.receiverEmail)}&redirect=${encodeURIComponent(redirectPath)}`);
          } else {
            router.push(`/register?email=${encodeURIComponent(data.receiverEmail)}&redirect=${encodeURIComponent(redirectPath)}`);
          }
        }
      } catch (err: any) {
        setError(err.message || "Liên kết lời mời đã hết hạn hoặc không tồn tại.");
      } finally {
        setIsLoading(false);
      }
    };

    fetchInviteInfo();
  }, [token, router]);

  const handleRespond = async (action: "accept" | "decline") => {
    if (!inviteInfo) return;
    const authToken = getToken();
    if (!authToken) {
      toast.error("Vui lòng đăng nhập để thực hiện thao tác");
      return;
    }

    try {
      setIsResponding(true);
      const res = await fetch("/api/friends/respond", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${authToken}`,
        },
        body: JSON.stringify({
          friendshipId: inviteInfo.id,
          action,
        }),
      });

      const data = await res.json();
      if (!res.ok) {
        throw new Error(data.message || "Xử lý lời mời thất bại");
      }

      setResolvedStatus(action === "accept" ? "accepted" : "declined");
      if (action === "accept") {
        toast.success("Chúc mừng! Bạn và đối phương đã trở thành bạn bè!");
      } else {
        toast.info("Đã từ chối lời mời kết bạn.");
      }
    } catch (err: any) {
      toast.error(err.message || "Có lỗi xảy ra khi xử lý lời mời");
    } finally {
      setIsResponding(false);
    }
  };

  if (isLoading) {
    return (
      <div className="flex min-h-screen flex-col items-center justify-center bg-muted/20 p-4">
        <Loader2 className="h-8 w-8 animate-spin text-primary mb-3" />
        <p className="text-sm text-muted-foreground animate-pulse">
          Đang xác thực lời mời kết bạn...
        </p>
      </div>
    );
  }

  if (error || !inviteInfo) {
    return (
      <div className="flex min-h-screen items-center justify-center bg-muted/20 p-4">
        <div className="w-full max-w-md bg-card rounded-2xl border border-border p-8 text-center shadow-lg space-y-4">
          <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-red-100 text-red-600 dark:bg-red-950/40 dark:text-red-400">
            <AlertTriangle className="h-7 w-7" />
          </div>
          <h2 className="text-lg font-bold text-foreground">Lời mời không khả dụng</h2>
          <p className="text-sm text-muted-foreground">
            {error || "Lời mời kết bạn này có thể đã được xử lý hoặc liên kết không còn hiệu lực."}
          </p>
          <div className="pt-2">
            <Link href="/">
              <Button variant="outline" className="w-full text-xs">
                Quay về trang chủ
              </Button>
            </Link>
          </div>
        </div>
      </div>
    );
  }

  // If user is not logged in yet (or if redirect didn't trigger immediately)
  if (!isLoggedIn) {
    const redirectUrl = `/friends/invite?token=${encodeURIComponent(token || "")}`;
    return (
      <div className="flex min-h-screen items-center justify-center bg-muted/20 p-4">
        <div className="w-full max-w-md bg-card rounded-2xl border border-border p-8 text-center shadow-lg space-y-5">
          <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-primary/10 text-primary">
            <UserPlus className="h-7 w-7" />
          </div>

          <div className="space-y-1.5">
            <h2 className="text-xl font-bold text-foreground">
              Lời mời kết bạn từ {inviteInfo.senderUsername}
            </h2>
            <p className="text-sm text-muted-foreground">
              {inviteInfo.senderUsername} ({inviteInfo.senderEmail}) muốn kết nối cùng bạn trên LOSM để cùng tìm kiếm điểm hẹn lý tưởng.
            </p>
          </div>

          <div className="rounded-xl bg-muted/50 p-4 text-xs text-muted-foreground text-left space-y-1">
            <p className="font-semibold text-foreground">Địa chỉ nhận lời mời:</p>
            <p className="font-mono text-primary font-medium">{inviteInfo.receiverEmail}</p>
          </div>

          {inviteInfo.userExists ? (
            <div className="space-y-2 pt-2">
              <Link
                href={`/login?email=${encodeURIComponent(inviteInfo.receiverEmail)}&redirect=${encodeURIComponent(redirectUrl)}`}
                className="w-full"
              >
                <Button className="w-full gap-2 bg-primary text-primary-foreground hover:bg-primary/90">
                  <LogIn className="h-4 w-4" />
                  <span>Đăng nhập để xem lời mời</span>
                </Button>
              </Link>
              <p className="text-xs text-muted-foreground">
                Tài khoản của bạn đã tồn tại trên hệ thống.
              </p>
            </div>
          ) : (
            <div className="space-y-2 pt-2">
              <Link
                href={`/register?email=${encodeURIComponent(inviteInfo.receiverEmail)}&redirect=${encodeURIComponent(redirectUrl)}`}
                className="w-full"
              >
                <Button className="w-full gap-2 bg-primary text-primary-foreground hover:bg-primary/90">
                  <UserPlus className="h-4 w-4" />
                  <span>Tạo tài khoản & xem lời mời</span>
                </Button>
              </Link>
              <p className="text-xs text-muted-foreground">
                Bạn chưa có tài khoản LOSM. Đăng ký chỉ mất 30 giây!
              </p>
            </div>
          )}
        </div>
      </div>
    );
  }

  // Already accepted in this session
  if (resolvedStatus === "accepted" || inviteInfo.status === "Accepted") {
    return (
      <div className="flex min-h-screen items-center justify-center bg-muted/20 p-4">
        <div className="w-full max-w-md bg-card rounded-2xl border border-border p-8 text-center shadow-xl space-y-5 animate-in zoom-in-95 duration-200">
          <div className="mx-auto flex h-16 w-16 items-center justify-center rounded-2xl bg-emerald-100 text-emerald-600 dark:bg-emerald-950/40 dark:text-emerald-400">
            <Sparkles className="h-8 w-8" />
          </div>

          <div className="space-y-2">
            <h2 className="text-xl font-bold text-foreground">
              Đã trở thành bạn bè!
            </h2>
            <p className="text-sm text-muted-foreground">
              Bạn và <strong className="text-foreground">{inviteInfo.senderUsername}</strong> đã kết nối thành công. Giờ đây hai bạn có thể chọn nhau khi tìm điểm hẹn chung công bằng.
            </p>
          </div>

          <div className="pt-2 flex flex-col gap-2.5">
            <Link href="/" className="w-full">
              <Button className="w-full gap-2 bg-primary text-primary-foreground hover:bg-primary/90">
                <MapPin className="h-4 w-4" />
                <span>Tìm điểm hẹn chung ngay</span>
              </Button>
            </Link>
            <Link href="/profile/friends" className="w-full">
              <Button variant="outline" className="w-full gap-2 text-xs">
                <Users className="h-4 w-4" />
                <span>Xem danh sách bạn bè</span>
              </Button>
            </Link>
          </div>
        </div>
      </div>
    );
  }

  // Declined
  if (resolvedStatus === "declined" || inviteInfo.status === "Declined") {
    return (
      <div className="flex min-h-screen items-center justify-center bg-muted/20 p-4">
        <div className="w-full max-w-md bg-card rounded-2xl border border-border p-8 text-center shadow-lg space-y-4">
          <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-muted text-muted-foreground">
            <X className="h-7 w-7" />
          </div>
          <h2 className="text-lg font-bold text-foreground">Đã từ chối lời mời</h2>
          <p className="text-sm text-muted-foreground">
            Bạn đã từ chối lời mời kết bạn từ {inviteInfo.senderUsername}.
          </p>
          <div className="pt-2">
            <Link href="/">
              <Button variant="outline" className="w-full text-xs">
                Về trang chủ
              </Button>
            </Link>
          </div>
        </div>
      </div>
    );
  }

  // Default: Pending and Logged-in -> Show Accept / Decline Card
  return (
    <div className="flex min-h-screen items-center justify-center bg-muted/20 p-4">
      <div className="w-full max-w-md bg-card rounded-2xl border border-border p-8 text-center shadow-2xl space-y-6">
        <div className="flex flex-col items-center">
          {inviteInfo.senderAvatarUrl ? (
            <img
              src={
                inviteInfo.senderAvatarUrl.startsWith("http")
                  ? `/api/avatar?url=${encodeURIComponent(inviteInfo.senderAvatarUrl)}`
                  : inviteInfo.senderAvatarUrl
              }
              alt={inviteInfo.senderUsername}
              className="h-20 w-20 rounded-full object-cover border-4 border-background shadow-md mb-3"
            />
          ) : (
            <div className="flex h-20 w-20 items-center justify-center rounded-full bg-primary/10 text-primary font-bold text-2xl border-4 border-background shadow-md mb-3">
              {inviteInfo.senderUsername.slice(0, 2).toUpperCase()}
            </div>
          )}

          <span className="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-xs font-semibold bg-primary/10 text-primary mb-2">
            <UserPlus className="h-3.5 w-3.5" />
            Lời mời kết bạn
          </span>

          <h2 className="text-xl font-bold text-foreground">
            {inviteInfo.senderUsername}
          </h2>
          <p className="text-xs text-muted-foreground">
            {inviteInfo.senderEmail}
          </p>
        </div>

        <div className="rounded-xl bg-muted/40 p-4 text-xs text-muted-foreground text-center border border-border/50">
          <p>
            {inviteInfo.senderUsername} muốn kết bạn cùng bạn trên LOSM để cùng nhau lập kế hoạch gặp mặt và tìm điểm hẹn tối ưu khoảng cách di chuyển.
          </p>
        </div>

        <div className="flex items-center gap-3 pt-2">
          <Button
            variant="outline"
            disabled={isResponding}
            onClick={() => handleRespond("decline")}
            className="flex-1 text-xs text-muted-foreground hover:text-red-600 hover:border-red-200"
          >
            <X className="mr-1.5 h-4 w-4" />
            <span>Từ chối</span>
          </Button>

          <Button
            disabled={isResponding}
            onClick={() => handleRespond("accept")}
            className="flex-1 text-xs bg-primary text-primary-foreground hover:bg-primary/90 gap-1.5"
          >
            {isResponding ? (
              <Loader2 className="h-4 w-4 animate-spin" />
            ) : (
              <Check className="h-4 w-4" />
            )}
            <span>Đồng ý kết bạn</span>
          </Button>
        </div>
      </div>
    </div>
  );
}

export default function FriendInvitePage() {
  return (
    <Suspense
      fallback={
        <div className="flex min-h-screen items-center justify-center bg-muted/20">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      }
    >
      <InviteContent />
    </Suspense>
  );
}
