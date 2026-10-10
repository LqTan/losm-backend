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
import { clearAuth, getUser } from "@/lib/auth";
import type { StoredUser } from "@/lib/auth";
import {
  Calendar,
  LogIn,
  LogOut,
  User as UserIcon,
} from "lucide-react";

export default function UserNavDropdown() {
  const [user, setUser] = useState<StoredUser | null>(null);
  const [mounted, setMounted] = useState(false);
  const router = useRouter();

  useEffect(() => {
    setMounted(true);
    setUser(getUser());
  }, []);

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
    <div className="fixed top-4 right-4 z-[1000]">
      <DropdownMenu>
        <DropdownMenuTrigger
          className="h-11 px-3 rounded-xl bg-card/95 border shadow-md flex items-center gap-2 backdrop-blur-md hover:bg-muted outline-none cursor-pointer transition text-left"
          title="Tài khoản cá nhân"
        >
          <div className="flex h-7 w-7 items-center justify-center rounded-lg bg-foreground text-background text-xs font-bold shrink-0">
            {initials}
          </div>
          <span className="hidden sm:inline-block max-w-[100px] truncate text-xs font-medium text-foreground">
            {user.username}
          </span>
        </DropdownMenuTrigger>

        <DropdownMenuContent align="end" className="w-56 p-1.5 shadow-xl">
          <div className="p-2">
            <div className="flex flex-col space-y-1">
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
  );
}
