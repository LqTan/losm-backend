"use client";

import { useEffect, useState } from "react";
import Link from "next/link";
import { useRouter } from "next/navigation";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuGroup,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { clearAuth, getUser, getToken, AUTH_CHANGE_EVENT } from "@/lib/auth";
import type { StoredUser } from "@/lib/auth";
import {
  Bell,
  Calendar,
  LogIn,
  LogOut,
  User as UserIcon,
  Users,
} from "lucide-react";
import FriendRequestsModal from "@/components/friends/FriendRequestsModal";
import { api } from "@/lib/api";

export default function UserNavDropdown() {
  const [user, setUser] = useState<StoredUser | null>(null);
  const [mounted, setMounted] = useState(false);
  const [pendingCount, setPendingCount] = useState(0);
  const [isRequestsModalOpen, setIsRequestsModalOpen] = useState(false);
  const router = useRouter();

  useEffect(() => {
    setMounted(true);

    function syncUser() {
      const u = getUser();
      setUser(u);
      if (u) {
        loadPendingCount();
      } else {
        setPendingCount(0);
      }
    }

    syncUser();

    window.addEventListener(AUTH_CHANGE_EVENT, syncUser);
    window.addEventListener("storage", syncUser);
    return () => {
      window.removeEventListener(AUTH_CHANGE_EVENT, syncUser);
      window.removeEventListener("storage", syncUser);
    };
  }, []);

  async function loadPendingCount() {
    try {
      const token = getToken();
      if (!token) return;
      const res = await api.get<{ incomingCount: number }>("/api/friends/requests", token);
      if (typeof res?.incomingCount === "number") {
        setPendingCount(res.incomingCount);
      }
    } catch {
      // ignore
    }
  }

  function handleLogout() {
    clearAuth();
    setUser(null);
    router.push("/login");
  }

  if (!mounted) {
    return (
      <div className="fixed top-4 right-4 z-[1000] h-11 w-11 rounded-xl bg-card/90 border shadow-md animate-pulse" />
    );
  }

  if (!user) {
    return (
      <div className="fixed top-4 right-4 z-[1000]">
        <Link href="/login">
          <Button
            variant="outline"
            size="sm"
            className="h-11 px-3.5 rounded-xl bg-card/95 border shadow-md font-medium text-xs gap-1.5 backdrop-blur-md hover:bg-muted"
          >
            <LogIn className="h-4 w-4 text-muted-foreground" />
            <span>Đăng nhập</span>
          </Button>
        </Link>
      </div>
    );
  }

  const initials = user.username
    ? user.username.slice(0, 2).toUpperCase()
    : "US";

  return (
    <>
      <div className="fixed top-4 right-4 z-[1000] flex items-center gap-2">
        {/* Nút Chuông thông báo lời mời kết bạn */}
        <button
          type="button"
          onClick={() => setIsRequestsModalOpen(true)}
          className="relative h-11 w-11 rounded-xl bg-card/95 border shadow-md flex items-center justify-center text-foreground backdrop-blur-md hover:bg-muted outline-none cursor-pointer transition"
          title={pendingCount > 0 ? `Có ${pendingCount} lời mời kết bạn mới` : "Thông báo lời mời kết bạn"}
        >
          <Bell className="h-4.5 w-4.5 text-muted-foreground" />
          {pendingCount > 0 && (
            <span className="absolute -top-1 -right-1 flex h-5 min-w-5 items-center justify-center px-1 rounded-full bg-red-500 text-white text-[10px] font-bold shadow-md animate-pulse">
              {pendingCount > 9 ? "9+" : pendingCount}
            </span>
          )}
        </button>

        <DropdownMenu>
          <DropdownMenuTrigger
            className="h-11 px-3 rounded-xl bg-card/95 border shadow-md flex items-center gap-2 backdrop-blur-md hover:bg-muted outline-none cursor-pointer transition text-left"
            title="Tài khoản cá nhân"
          >
            {user.avatarUrl ? (
              <img
                src={user.avatarUrl.startsWith("http") ? `/api/avatar?url=${encodeURIComponent(user.avatarUrl)}` : user.avatarUrl}
                alt={user.username}
                className="h-7 w-7 rounded-lg object-cover border border-border shrink-0"
              />
            ) : (
              <div className="flex h-7 w-7 items-center justify-center rounded-lg bg-foreground text-background text-xs font-bold shrink-0">
                {initials}
              </div>
            )}
            <span className="hidden sm:inline-block max-w-[100px] truncate text-xs font-medium text-foreground">
              {user.username}
            </span>
          </DropdownMenuTrigger>

          <DropdownMenuContent align="end" className="w-56 p-1.5 shadow-xl">
            <div className="p-2">
              <div className="flex items-center gap-2.5">
                {user.avatarUrl ? (
                  <img
                    src={user.avatarUrl.startsWith("http") ? `/api/avatar?url=${encodeURIComponent(user.avatarUrl)}` : user.avatarUrl}
                    alt={user.username}
                    className="h-9 w-9 rounded-xl object-cover border border-border shrink-0"
                  />
                ) : (
                  <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-foreground text-background text-sm font-bold shrink-0">
                    {initials}
                  </div>
                )}
                <div className="min-w-0 flex-1">
                  <div className="flex items-center justify-between">
                    <p className="text-sm font-semibold text-foreground truncate">
                      {user.username}
                    </p>
                    <Badge variant="secondary" className="text-[10px] px-1.5 py-0 font-normal">
                      {user.role || "Thành viên"}
                    </Badge>
                  </div>
                  <p className="text-xs text-muted-foreground truncate">{user.email}</p>
                </div>
              </div>
            </div>

            <DropdownMenuSeparator />

            <DropdownMenuGroup>
              <DropdownMenuItem
                onClick={() => router.push("/profile")}
                className="cursor-pointer text-xs flex items-center gap-2"
              >
                <UserIcon className="h-4 w-4 text-muted-foreground" />
                <span>Hồ sơ cá nhân</span>
              </DropdownMenuItem>

              <DropdownMenuItem
                onClick={() => router.push("/profile/friends")}
                className="cursor-pointer text-xs flex items-center justify-between"
              >
                <div className="flex items-center gap-2">
                  <Users className="h-4 w-4 text-muted-foreground" />
                  <span>Bạn bè</span>
                </div>
                {pendingCount > 0 && (
                  <span className="flex h-4 min-w-4 items-center justify-center px-1 rounded-full bg-red-500 text-white text-[9px] font-bold">
                    {pendingCount}
                  </span>
                )}
              </DropdownMenuItem>

              <DropdownMenuItem
                onClick={() => router.push("/profile/calendar")}
                className="cursor-pointer text-xs flex items-center gap-2"
              >
                <Calendar className="h-4 w-4 text-muted-foreground" />
                <span>Lịch hẹn của tôi</span>
              </DropdownMenuItem>
            </DropdownMenuGroup>

            <DropdownMenuSeparator />

            <DropdownMenuItem
              onClick={handleLogout}
              className="cursor-pointer text-xs text-red-600 focus:text-red-600 focus:bg-red-50 dark:focus:bg-red-950/30 flex items-center gap-2"
            >
              <LogOut className="h-4 w-4" />
              <span>Đăng xuất</span>
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>

      <FriendRequestsModal
        isOpen={isRequestsModalOpen}
        onClose={() => setIsRequestsModalOpen(false)}
        onRequestsUpdated={(count) => setPendingCount(count)}
      />
    </>
  );
}
