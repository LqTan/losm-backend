"use client";

import { useEffect, useState } from "react";
import { api, ApiError } from "@/lib/api";
import { getToken, getUser, setAuth } from "@/lib/auth";
import type { StoredUser } from "@/lib/auth";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { toast } from "sonner";
import { Calendar, Check, Loader2, Mail, Shield, User } from "lucide-react";
import Link from "next/link";

interface UserProfileData {
  id: string;
  username: string;
  email: string;
  role: string;
  createdAt?: string;
}

export default function ProfilePage() {
  const [currentUser, setCurrentUser] = useState<StoredUser | null>(null);
  const [usernameInput, setUsernameInput] = useState("");
  const [loading, setLoading] = useState(true);
  const [saving, setSaving] = useState(false);
  const [savedSuccess, setSavedSuccess] = useState(false);
  const [meetingsCount, setMeetingsCount] = useState<number | null>(null);

  useEffect(() => {
    async function loadData() {
      const stored = getUser();
      setCurrentUser(stored);
      if (stored?.username) {
        setUsernameInput(stored.username);
      }

      const token = getToken();
      if (!token) return;

      try {
        const profile = await api.get<UserProfileData>("/api/users/me", token);
        if (profile) {
          setCurrentUser((prev) => ({
            id: profile.id,
            username: profile.username,
            email: profile.email,
            role: profile.role || prev?.role || "User",
          }));
          setUsernameInput(profile.username);
        }
      } catch {
        // Fallback to local user
      }

      // Fetch meeting count
      try {
        const meetings = await api.get<any[]>("/api/meetings", token);
        if (Array.isArray(meetings)) {
          setMeetingsCount(meetings.length);
        }
      } catch {
        setMeetingsCount(0);
      } finally {
        setLoading(false);
      }
    }

    void loadData();
  }, []);

  async function handleSaveProfile(e: React.FormEvent) {
    e.preventDefault();
    if (!usernameInput.trim()) {
      toast.error("Tên người dùng không được để trống.");
      return;
    }

    setSaving(true);
    setSavedSuccess(false);
    try {
      const token = getToken();
      await api.put<{ id: string; username: string }>(
        "/api/users/me",
        { username: usernameInput.trim() },
        token,
      );

      if (currentUser && token) {
        const updated = { ...currentUser, username: usernameInput.trim() };
        setCurrentUser(updated);
        setAuth(token, updated.role, updated);
      }

      setSavedSuccess(true);
      toast.success("Cập nhật thông tin hồ sơ thành công!");
      setTimeout(() => setSavedSuccess(false), 3000);
    } catch (err) {
      toast.error(
        err instanceof ApiError
          ? err.message
          : "Không thể cập nhật thông tin hồ sơ.",
      );
    } finally {
      setSaving(false);
    }
  }

  if (loading) {
    return (
      <div className="flex h-64 items-center justify-center">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-6">
      <div>
        <h2 className="text-xl font-bold tracking-tight text-foreground">Hồ sơ của bạn</h2>
        <p className="text-xs sm:text-sm text-muted-foreground mt-0.5">
          Quản lý thông tin tài khoản cá nhân và các hoạt động trên hệ thống LOSM.
        </p>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-6">
        {/* Main Profile Info Form */}
        <Card className="md:col-span-2 shadow-sm border bg-card">
          <CardHeader className="pb-4">
            <CardTitle className="text-base font-semibold flex items-center gap-2">
              <User className="h-4 w-4 text-muted-foreground" />
              <span>Thông tin tài khoản</span>
            </CardTitle>
            <CardDescription className="text-xs">
              Thay đổi tên hiển thị để bạn bè dễ dàng nhận diện trong các cuộc hẹn.
            </CardDescription>
          </CardHeader>
          <CardContent>
            <form onSubmit={handleSaveProfile} className="space-y-4">
              <div className="space-y-1.5">
                <Label htmlFor="username" className="text-xs font-medium">
                  Tên hiển thị (Username)
                </Label>
                <Input
                  id="username"
                  value={usernameInput}
                  onChange={(e) => setUsernameInput(e.target.value)}
                  placeholder="Nhập tên người dùng..."
                  className="h-9 text-sm"
                  required
                />
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="email" className="text-xs font-medium">
                  Địa chỉ Email
                </Label>
                <div className="relative">
                  <Input
                    id="email"
                    value={currentUser?.email ?? ""}
                    disabled
                    className="h-9 text-sm bg-muted/50 cursor-not-allowed pl-9"
                  />
                  <Mail className="h-4 w-4 text-muted-foreground absolute left-3 top-2.5" />
                </div>
                <p className="text-[11px] text-muted-foreground">
                  Email dùng để đăng nhập và nhận thông báo thư mời hẹn gặp.
                </p>
              </div>

              <div className="space-y-1.5">
                <Label className="text-xs font-medium">Vai trò hệ thống</Label>
                <div>
                  <Badge variant="outline" className="text-xs py-1 px-2.5 gap-1.5 font-normal">
                    <Shield className="h-3.5 w-3.5 text-muted-foreground" />
                    <span>{currentUser?.role === "Admin" ? "Quản trị viên (Admin)" : "Thành viên (User)"}</span>
                  </Badge>
                </div>
              </div>

              <div className="pt-2 flex items-center gap-3">
                <Button
                  type="submit"
                  disabled={saving || !usernameInput.trim()}
                  className="h-9 px-4 text-xs font-medium gap-1.5"
                >
                  {saving ? (
                    <>
                      <Loader2 className="h-3.5 w-3.5 animate-spin" />
                      <span>Đang lưu...</span>
                    </>
                  ) : savedSuccess ? (
                    <>
                      <Check className="h-3.5 w-3.5" />
                      <span>Đã lưu</span>
                    </>
                  ) : (
                    <span>Lưu thay đổi</span>
                  )}
                </Button>
              </div>
            </form>
          </CardContent>
        </Card>

        {/* Side Overview / Quick Stats Card */}
        <div className="space-y-6">
          <Card className="shadow-sm border bg-card">
            <CardHeader className="pb-3">
              <CardTitle className="text-sm font-semibold flex items-center gap-2">
                <Calendar className="h-4 w-4 text-muted-foreground" />
                <span>Hoạt động lịch hẹn</span>
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div className="rounded-xl border bg-muted/30 p-3.5 flex items-center justify-between">
                <div>
                  <p className="text-xs text-muted-foreground">Lịch hẹn đã tham gia/tạo</p>
                  <p className="text-2xl font-bold text-foreground mt-0.5">
                    {meetingsCount !== null ? meetingsCount : "..."}
                  </p>
                </div>
                <div className="flex h-10 w-10 items-center justify-center rounded-lg bg-card border text-muted-foreground">
                  <Calendar className="h-5 w-5" />
                </div>
              </div>

              <Link href="/profile/calendar">
                <Button
                  variant="outline"
                  className="w-full text-xs font-medium h-9 gap-1.5 hover:bg-muted"
                >
                  <span>Mở Google Calendar view</span>
                </Button>
              </Link>
            </CardContent>
          </Card>

          <Card className="shadow-sm border bg-card text-xs text-muted-foreground p-4 space-y-2">
            <p className="font-semibold text-foreground">💡 Lưu ý bảo mật</p>
            <p>
              Mỗi lịch hẹn bạn tạo sẽ tự động được gửi kèm tệp lịch .ics đến hộp thư của những người tham gia để tự động đồng bộ lên Google Calendar, Apple Calendar hay Outlook.
            </p>
          </Card>
        </div>
      </div>
    </div>
  );
}
