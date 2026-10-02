"use client";

import { useCallback, useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { ArrowLeft, Calendar, Eye, EyeOff, Mail, Save, Sparkles, User } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Badge } from "@/components/ui/badge";
import { Hairline } from "@/shared/ui/hairline";
import { InlineError } from "@/shared/ui/inline-error";
import { COPY } from "@/shared/config/copy";
import { useCurrentUser } from "@/features/current-user/use-current-user";
import { useUpdateProfile } from "@/features/update-profile/use-update-profile";
import { useGoogleCalendar } from "@/features/google-calendar/use-google-calendar";
import { toast } from "sonner";

const SMTP_STORAGE_KEY = "losm.settings.smtp";
const TEMPLATE_HTML_DEFAULT = `<!doctype html>
<html><body style="font-family:system-ui,sans-serif;background:#f6f6f6;padding:24px;">
  <div style="max-width:560px;margin:0 auto;background:#fff;padding:24px;border-radius:8px;">
    <h2 style="margin:0 0 8px;font-size:18px;">Lời mời họp: {{title}}</h2>
    <p style="margin:8px 0;color:#555;">Xin chào {{attendeeName}},</p>
    <p style="margin:8px 0;color:#555;">Bạn được mời tham dự cuộc hẹn:</p>
    <ul style="margin:8px 0;color:#333;line-height:1.6;">
      <li><b>Thời gian:</b> {{startAt}}</li>
      <li><b>Địa điểm:</b> {{placeName}}</li>
      <li><b>Địa chỉ:</b> {{address}}</li>
      <li><b>Mục đích:</b> {{purpose}}</li>
    </ul>
    <p style="margin:16px 0;color:#555;">Đính kèm file .ics để thêm vào lịch.</p>
    <p style="margin:8px 0;color:#999;font-size:12px;">Gửi từ LocationSearch</p>
  </div>
</body></html>`;

const TEMPLATE_TEXT_DEFAULT = `Lời mời họp: {{title}}

Xin chào {{attendeeName}},

Bạn được mời tham dự cuộc hẹn:
- Thời gian: {{startAt}}
- Địa điểm: {{placeName}}
- Địa chỉ: {{address}}
- Mục đích: {{purpose}}

Đính kèm file .ics để thêm vào lịch.

— LocationSearch`;

interface SmtpForm {
  provider: string;
  host: string;
  port: number;
  useStartTls: boolean;
  user: string;
  password: string;
  fromName: string;
  fromAddress: string;
}

const SMTP_DEFAULT: SmtpForm = {
  provider: "smtp",
  host: "smtp.gmail.com",
  port: 587,
  useStartTls: true,
  user: "",
  password: "",
  fromName: "LocationSearch",
  fromAddress: "",
};

export interface SettingsViewProps {
  readonly initialUser: {
    readonly id: string;
    readonly email: string;
    readonly username: string;
    readonly displayName: string;
    readonly createdAt: string;
  };
}

export function SettingsView({ initialUser }: SettingsViewProps) {
  const router = useRouter();
  const user = useCurrentUser(initialUser as never);
  const profile = useUpdateProfile();
  const google = useGoogleCalendar();

  const [username, setUsername] = useState(initialUser.username);
  const [displayName, setDisplayName] = useState(initialUser.displayName);

  const [smtp, setSmtp] = useState<SmtpForm>(SMTP_DEFAULT);
  const [showPassword, setShowPassword] = useState(false);

  const [templateSubject, setTemplateSubject] = useState("[LocationSearch] {{title}}");
  const [templateHtml, setTemplateHtml] = useState(TEMPLATE_HTML_DEFAULT);
  const [templateText, setTemplateText] = useState(TEMPLATE_TEXT_DEFAULT);

  useEffect(() => {
    if (typeof window === "undefined") return;
    const raw = window.localStorage.getItem(SMTP_STORAGE_KEY);
    if (!raw) return;
    try {
      const parsed = JSON.parse(raw) as Partial<SmtpForm>;
      setSmtp((s) => ({ ...s, ...parsed }));
    } catch {
      /* ignore */
    }
  }, []);

  const renderPreviewHtml = useCallback(() => {
    return templateHtml
      .replace(/\{\{title\}\}/g, "Họp khách A")
      .replace(/\{\{attendeeName\}\}/g, "Anh A")
      .replace(/\{\{startAt\}\}/g, "Thứ Sáu 18:00, 18/10/2026")
      .replace(/\{\{placeName\}\}/g, "Highlands Coffee Vincom")
      .replace(/\{\{address\}\}/g, "72 Lê Thánh Tôn, Quận 1, TP.HCM")
      .replace(/\{\{purpose\}\}/g, "Bàn hợp đồng Q4");
  }, [templateHtml]);

  const handleSaveProfile = useCallback(async () => {
    try {
      await profile.update(username.trim());
      toast.success("Đã lưu hồ sơ.");
    } catch {
      /* toast handled */
    }
  }, [profile, username]);

  const handleSaveSmtp = useCallback(() => {
    window.localStorage.setItem(SMTP_STORAGE_KEY, JSON.stringify(smtp));
    toast.success("Đã lưu cấu hình mail (mock).");
  }, [smtp]);

  const handleTestSmtp = useCallback(() => {
    if (!smtp.host || !smtp.port) {
      toast.error("Vui lòng nhập host và port.");
      return;
    }
    toast.success(`Đã gửi test tới ${smtp.user || "(chưa có user)"} (mock).`);
  }, [smtp]);

  const handleSaveTemplate = useCallback(() => {
    window.localStorage.setItem(
      "losm.settings.templates.meeting-invitation.subject",
      templateSubject,
    );
    window.localStorage.setItem(
      "losm.settings.templates.meeting-invitation.html",
      templateHtml,
    );
    window.localStorage.setItem(
      "losm.settings.templates.meeting-invitation.text",
      templateText,
    );
    toast.success("Đã lưu template (mock).");
  }, [templateSubject, templateHtml, templateText]);

  const handleResetTemplate = useCallback(() => {
    setTemplateSubject("[LocationSearch] {{title}}");
    setTemplateHtml(TEMPLATE_HTML_DEFAULT);
    setTemplateText(TEMPLATE_TEXT_DEFAULT);
    toast.message("Đã reset về mặc định.");
  }, []);

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="flex h-12 items-center justify-between border-b px-4">
        <div className="flex items-center gap-2">
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            aria-label="Về trang chính"
            onClick={() => router.push("/")}
          >
            <ArrowLeft size={16} strokeWidth={1.5} />
          </Button>
          <Hairline orientation="vertical" className="h-6" />
          <p className="text-[14px] font-semibold">Cài đặt</p>
        </div>
      </header>

      <div className="mx-auto w-full max-w-3xl flex-1 overflow-auto px-4 py-6">
        <Tabs defaultValue="account">
          <TabsList className="grid w-full grid-cols-4">
            <TabsTrigger value="account" className="gap-1.5">
              <User size={14} strokeWidth={1.5} />
              Tài khoản
            </TabsTrigger>
            <TabsTrigger value="calendar" className="gap-1.5">
              <Calendar size={14} strokeWidth={1.5} />
              Calendar
            </TabsTrigger>
            <TabsTrigger value="mail" className="gap-1.5">
              <Mail size={14} strokeWidth={1.5} />
              Mail
            </TabsTrigger>
            <TabsTrigger value="templates" className="gap-1.5">
              <Sparkles size={14} strokeWidth={1.5} />
              Templates
            </TabsTrigger>
          </TabsList>

          <TabsContent value="account" className="mt-4">
            <Card>
              <CardHeader>
                <CardTitle>Hồ sơ</CardTitle>
                <CardDescription>
                  Thông tin hiển thị trong app và email mời họp.
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <div className="flex flex-col gap-1">
                  <Label htmlFor="settings-email">Email</Label>
                  <Input
                    id="settings-email"
                    value={user.data?.user.email ?? initialUser.email}
                    disabled
                  />
                </div>
                <div className="flex flex-col gap-1">
                  <Label htmlFor="settings-username">Username</Label>
                  <Input
                    id="settings-username"
                    value={username}
                    onChange={(e) => setUsername(e.target.value)}
                  />
                </div>
                <div className="flex flex-col gap-1">
                  <Label htmlFor="settings-display-name">Tên hiển thị</Label>
                  <Input
                    id="settings-display-name"
                    value={displayName}
                    onChange={(e) => setDisplayName(e.target.value)}
                  />
                </div>
                {profile.error ? <InlineError message={profile.error} /> : null}
                <div className="flex justify-end">
                  <Button onClick={() => void handleSaveProfile()} disabled={profile.updating}>
                    <Save size={14} strokeWidth={1.5} />
                    {profile.updating ? "Đang lưu…" : "Lưu"}
                  </Button>
                </div>
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="calendar" className="mt-4">
            <Card>
              <CardHeader>
                <CardTitle>Google Calendar</CardTitle>
                <CardDescription>
                  Khi kết nối, lịch hẹn sẽ tự động tạo event trên Google Calendar của bạn.
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                {google.loading ? (
                  <p className="text-[13px] text-muted-foreground">Đang kiểm tra…</p>
                ) : google.error ? (
                  <InlineError message={google.error} />
                ) : google.status?.connected ? (
                  <div className="flex items-center justify-between rounded-md border bg-muted/30 p-3">
                    <div>
                      <p className="text-[14px] font-medium">
                        Đã kết nối {google.status.googleEmail}
                      </p>
                      <p className="text-[11px] text-muted-foreground">
                        Hết hạn: {google.status.accessTokenExpiresAt
                          ? new Date(google.status.accessTokenExpiresAt).toLocaleString("vi-VN")
                          : "—"}
                      </p>
                    </div>
                    <Button
                      type="button"
                      variant="secondary"
                      size="sm"
                      onClick={() => void google.disconnect()}
                      disabled={google.connecting}
                    >
                      Ngắt kết nối
                    </Button>
                  </div>
                ) : (
                  <div className="flex items-center justify-between rounded-md border bg-muted/30 p-3">
                    <p className="text-[13px] text-muted-foreground">
                      Chưa kết nối Google Calendar.
                    </p>
                    <Button
                      type="button"
                      size="sm"
                      onClick={() => google.connect()}
                      disabled={google.connecting}
                    >
                      {google.connecting ? "Đang chuyển…" : "Kết nối"}
                    </Button>
                  </div>
                )}
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="mail" className="mt-4">
            <Card>
              <CardHeader>
                <CardTitle>Cấu hình gửi mail</CardTitle>
                <CardDescription>
                  SMTP server để gửi lời mời họp qua email. (Mock — lưu local)
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-3">
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-3">
                  <div className="flex flex-col gap-1 sm:col-span-2">
                    <Label htmlFor="smtp-host">Host</Label>
                    <Input
                      id="smtp-host"
                      value={smtp.host}
                      onChange={(e) => setSmtp({ ...smtp, host: e.target.value })}
                      placeholder="smtp.gmail.com"
                    />
                  </div>
                  <div className="flex flex-col gap-1">
                    <Label htmlFor="smtp-port">Port</Label>
                    <Input
                      id="smtp-port"
                      type="number"
                      value={smtp.port}
                      onChange={(e) =>
                        setSmtp({ ...smtp, port: Number(e.target.value) || 0 })
                      }
                    />
                  </div>
                </div>
                <div className="flex flex-col gap-1">
                  <Label htmlFor="smtp-user">User</Label>
                  <Input
                    id="smtp-user"
                    value={smtp.user}
                    onChange={(e) => setSmtp({ ...smtp, user: e.target.value })}
                    placeholder="bot@gmail.com"
                  />
                </div>
                <div className="flex flex-col gap-1">
                  <Label htmlFor="smtp-password">Password</Label>
                  <div className="relative">
                    <Input
                      id="smtp-password"
                      type={showPassword ? "text" : "password"}
                      value={smtp.password}
                      onChange={(e) => setSmtp({ ...smtp, password: e.target.value })}
                      placeholder="App password"
                    />
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon-sm"
                      className="absolute right-1 top-1/2 -translate-y-1/2"
                      aria-label={showPassword ? "Ẩn mật khẩu" : "Hiện mật khẩu"}
                      onClick={() => setShowPassword((s) => !s)}
                    >
                      {showPassword ? (
                        <EyeOff size={14} strokeWidth={1.5} />
                      ) : (
                        <Eye size={14} strokeWidth={1.5} />
                      )}
                    </Button>
                  </div>
                </div>
                <div className="grid grid-cols-1 gap-3 sm:grid-cols-2">
                  <div className="flex flex-col gap-1">
                    <Label htmlFor="smtp-from-name">From name</Label>
                    <Input
                      id="smtp-from-name"
                      value={smtp.fromName}
                      onChange={(e) => setSmtp({ ...smtp, fromName: e.target.value })}
                    />
                  </div>
                  <div className="flex flex-col gap-1">
                    <Label htmlFor="smtp-from-address">From address</Label>
                    <Input
                      id="smtp-from-address"
                      value={smtp.fromAddress}
                      onChange={(e) =>
                        setSmtp({ ...smtp, fromAddress: e.target.value })
                      }
                      placeholder="bot@gmail.com"
                    />
                  </div>
                </div>
                <div className="flex justify-end gap-2">
                  <Button type="button" variant="secondary" onClick={handleTestSmtp}>
                    Test connection
                  </Button>
                  <Button type="button" onClick={handleSaveSmtp}>
                    <Save size={14} strokeWidth={1.5} />
                    Lưu
                  </Button>
                </div>
              </CardContent>
            </Card>
          </TabsContent>

          <TabsContent value="templates" className="mt-4">
            <div className="space-y-4">
              <Card>
                <CardHeader>
                  <CardTitle className="flex items-center gap-2">
                    Template lời mời họp
                    <Badge variant="secondary">meeting-invitation</Badge>
                  </CardTitle>
                  <CardDescription>
                    Biến khả dụng: {`{{title}}`}, {`{{attendeeName}}`}, {`{{startAt}}`},{" "}
                    {`{{placeName}}`}, {`{{address}}`}, {`{{purpose}}`}.
                  </CardDescription>
                </CardHeader>
                <CardContent className="space-y-3">
                  <div className="flex flex-col gap-1">
                    <Label htmlFor="tpl-subject">Subject</Label>
                    <Input
                      id="tpl-subject"
                      value={templateSubject}
                      onChange={(e) => setTemplateSubject(e.target.value)}
                    />
                  </div>
                  <div className="flex flex-col gap-1">
                    <Label htmlFor="tpl-html">HTML body</Label>
                    <Textarea
                      id="tpl-html"
                      value={templateHtml}
                      onChange={(e) => setTemplateHtml(e.target.value)}
                      rows={8}
                      className="font-mono text-[12px]"
                    />
                  </div>
                  <div className="flex flex-col gap-1">
                    <Label htmlFor="tpl-text">Text body</Label>
                    <Textarea
                      id="tpl-text"
                      value={templateText}
                      onChange={(e) => setTemplateText(e.target.value)}
                      rows={6}
                      className="font-mono text-[12px]"
                    />
                  </div>
                  <div className="flex justify-end gap-2">
                    <Button type="button" variant="secondary" onClick={handleResetTemplate}>
                      Reset
                    </Button>
                    <Button type="button" onClick={handleSaveTemplate}>
                      <Save size={14} strokeWidth={1.5} />
                      Lưu
                    </Button>
                  </div>
                </CardContent>
              </Card>

              <Card>
                <CardHeader>
                  <CardTitle>Preview</CardTitle>
                  <CardDescription>
                    Render với dữ liệu mẫu. Khi gửi thật sẽ thay bằng thông tin từ cuộc hẹn.
                  </CardDescription>
                </CardHeader>
                <CardContent className="p-0">
                  <iframe
                    title="Email preview"
                    srcDoc={renderPreviewHtml()}
                    className="h-[420px] w-full rounded-b-lg border-0"
                    sandbox=""
                  />
                </CardContent>
              </Card>
            </div>
          </TabsContent>
        </Tabs>

        <p className="mt-6 text-center text-[12px] text-muted-foreground">
          {COPY.app.tagline}
        </p>
      </div>
    </div>
  );
}
