"use client";

import {
  Sidebar,
  SidebarContent,
  SidebarFooter,
  SidebarGroup,
  SidebarGroupContent,
  SidebarGroupLabel,
  SidebarHeader,
  SidebarInset,
  SidebarMenu,
  SidebarMenuButton,
  SidebarMenuItem,
  SidebarProvider,
  SidebarTrigger,
} from "@/components/ui/sidebar";
import {
  Calendar,
  ChevronLeft,
  Home,
  LogOut,
  ShieldCheck,
  User as UserIcon,
} from "lucide-react";
import Link from "next/link";
import { usePathname, useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { clearAuth, getToken, getUser, isAdmin } from "@/lib/auth";
import type { StoredUser } from "@/lib/auth";
import { Button } from "@/components/ui/button";

const menuItems = [
  { href: "/profile", label: "Thông tin hồ sơ", icon: UserIcon },
  { href: "/profile/calendar", label: "Lịch hẹn của tôi", icon: Calendar },
];

export default function ProfileLayout({
  children,
}: {
  children: React.ReactNode;
}) {
  const pathname = usePathname();
  const router = useRouter();
  const [user, setUser] = useState<StoredUser | null>(null);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    const token = getToken();
    const stored = getUser();
    if (!token) {
      router.replace("/login");
      return;
    }
    setUser(stored);
    setReady(true);
  }, [router]);

  function handleLogout() {
    clearAuth();
    router.replace("/login");
  }

  if (!ready) {
    return (
      <div className="flex h-screen items-center justify-center bg-background">
        <p className="text-sm text-muted-foreground animate-pulse">Đang tải hồ sơ...</p>
      </div>
    );
  }

  const initials = user?.username ? user.username.slice(0, 2).toUpperCase() : "US";

  return (
    <SidebarProvider className="h-svh overflow-hidden bg-background">
      <Sidebar>
        <SidebarHeader className="border-b px-4 py-3">
          <div className="flex items-center justify-between">
            <div className="flex items-center gap-2">
              <div className="flex h-8 w-8 items-center justify-center rounded-lg bg-foreground text-background font-bold text-xs">
                {initials}
              </div>
              <div className="min-w-0">
                <p className="text-sm font-semibold text-foreground truncate">
                  {user?.username || "Người dùng"}
                </p>
                <p className="text-[11px] text-muted-foreground truncate">
                  {user?.email}
                </p>
              </div>
            </div>
          </div>
          <div className="mt-2">
            <Link href="/">
              <Button
                variant="outline"
                size="sm"
                className="w-full justify-start text-xs h-8 gap-1.5"
              >
                <ChevronLeft className="h-3.5 w-3.5" />
                <span>Quay lại Bản đồ</span>
              </Button>
            </Link>
          </div>
        </SidebarHeader>

        <SidebarContent>
          <SidebarGroup>
            <SidebarGroupLabel className="text-xs font-medium text-muted-foreground">
              Tài khoản & Lịch
            </SidebarGroupLabel>
            <SidebarGroupContent>
              <SidebarMenu>
                {menuItems.map((item) => {
                  const Icon = item.icon;
                  const active =
                    item.href === "/profile"
                      ? pathname === "/profile"
                      : pathname.startsWith(item.href);
                  return (
                    <SidebarMenuItem key={item.href}>
                      <SidebarMenuButton
                        isActive={active}
                        render={<Link href={item.href} />}
                      >
                        <Icon className="h-4 w-4" />
                        <span>{item.label}</span>
                      </SidebarMenuButton>
                    </SidebarMenuItem>
                  );
                })}
              </SidebarMenu>
            </SidebarGroupContent>
          </SidebarGroup>
        </SidebarContent>

        <SidebarFooter className="border-t p-3">
          <Button
            variant="ghost"
            size="sm"
            onClick={handleLogout}
            className="w-full justify-start text-xs text-red-600 hover:text-red-700 hover:bg-red-50 dark:hover:bg-red-950/30 gap-2"
          >
            <LogOut className="h-4 w-4" />
            <span>Đăng xuất</span>
          </Button>
        </SidebarFooter>
      </Sidebar>

      <SidebarInset className="flex flex-col overflow-hidden bg-background">
        <header className="flex h-14 items-center justify-between border-b px-4 shrink-0 bg-card">
          <div className="flex items-center gap-3">
            <SidebarTrigger />
            <h1 className="text-sm font-semibold text-foreground">
              {pathname === "/profile/calendar" ? "Lịch hẹn của tôi" : "Hồ sơ cá nhân"}
            </h1>
          </div>
          <Link href="/">
            <Button variant="ghost" size="sm" className="text-xs gap-1.5 text-muted-foreground hover:text-foreground">
              <Home className="h-3.5 w-3.5" />
              <span>Bản đồ</span>
            </Button>
          </Link>
        </header>

        <main className="flex-1 overflow-y-auto p-4 sm:p-6 bg-muted/20">
          <div className="mx-auto max-w-5xl">
            {children}
          </div>
        </main>
      </SidebarInset>
    </SidebarProvider>
  );
}
