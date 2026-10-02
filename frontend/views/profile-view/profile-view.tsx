"use client";

import { useCallback, useState } from "react";
import { useRouter } from "next/navigation";
import {
  ArrowLeft,
  Calendar,
  Check,
  Mail,
  MapPin,
  Pencil,
  Star,
  Trash2,
  X,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Card, CardContent } from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Badge } from "@/components/ui/badge";
import { Hairline } from "@/shared/ui/hairline";
import { InlineError } from "@/shared/ui/inline-error";
import { COPY } from "@/shared/config/copy";
import { useCurrentUser } from "@/features/current-user/use-current-user";
import { useSavedPlaces } from "@/features/saved-places-list/use-saved-places";
import { useUpdateProfile } from "@/features/update-profile/use-update-profile";
import { useMeetingsList } from "@/features/meetings-list/use-meetings-list";
import { useMyReviews } from "@/features/reviews-list/use-reviews-list";
import { useGoogleCalendar } from "@/features/google-calendar/use-google-calendar";
import { useRetryMeetingEmails } from "@/features/retry-meeting-emails/use-retry-meeting-emails";
import { logoutAction } from "@/features/auth";
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from "@/components/ui/alert-dialog";
import { formatDate, formatDateTime } from "@/shared/lib/formatters";
import { useQueryClient } from "@tanstack/react-query";
import { SESSION_LIST_QUERY_KEY } from "@/features/session-list/use-session-list";
import { SAVED_PLACES_LIST_QUERY_KEY } from "@/features/saved-places-list/use-saved-places";

export interface ProfileViewProps {
  readonly initialUser: {
    readonly id: string;
    readonly email: string;
    readonly username: string;
    readonly displayName: string;
    readonly createdAt: string;
  };
}

export function ProfileView({ initialUser }: ProfileViewProps) {
  const router = useRouter();
  const qc = useQueryClient();
  const user = useCurrentUser(initialUser as never);
  const profile = useUpdateProfile();
  const saved = useSavedPlaces();
  const meetings = useMeetingsList({ scope: "upcoming" });
  const meetingsPast = useMeetingsList({ scope: "past" });
  const reviews = useMyReviews(true);
  const google = useGoogleCalendar();
  const retryEmails = useRetryMeetingEmails();

  const [editing, setEditing] = useState(false);
  const [usernameDraft, setUsernameDraft] = useState(initialUser.username);

  const handleSaveUsername = useCallback(async () => {
    try {
      await profile.update(usernameDraft.trim());
      setEditing(false);
    } catch {
      /* error already shown via toast */
    }
  }, [profile, usernameDraft]);

  const handleLogout = useCallback(async () => {
    await logoutAction();
    void qc.invalidateQueries({ queryKey: [...SESSION_LIST_QUERY_KEY] });
    void qc.invalidateQueries({ queryKey: [...SAVED_PLACES_LIST_QUERY_KEY] });
    router.replace("/login");
    router.refresh();
  }, [router, qc]);

  const current = user.data?.user;
  if (!current) {
    return (
      <div className="min-h-dvh flex flex-col items-center justify-center gap-3 p-4">
        <InlineError message={COPY.profile.notLoggedIn} />
        <Button type="button" variant="secondary" onClick={() => router.push("/login")}>
          Về đăng nhập
        </Button>
      </div>
    );
  }

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="flex h-12 items-center justify-between border-b px-4">
        <div className="flex items-center gap-2">
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            aria-label={COPY.profile.back}
            onClick={() => router.push("/")}
          >
            <ArrowLeft size={16} strokeWidth={1.5} />
          </Button>
          <Hairline orientation="vertical" className="h-6" />
          <p className="text-[14px] font-semibold">{COPY.profile.title}</p>
        </div>
        <Button type="button" variant="ghost" size="sm" onClick={() => void handleLogout()}>
          <X size={14} strokeWidth={1.5} />
          {COPY.nav.logout}
        </Button>
      </header>

      <div className="mx-auto w-full max-w-3xl flex-1 space-y-6 overflow-auto px-4 py-6">
        <section className="flex flex-col gap-3">
          <p className="text-[11px] uppercase tracking-wider text-muted-foreground">
            Tài khoản
          </p>
          <Card>
            <CardContent className="space-y-3 p-4 text-[14px]">
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Email</span>
                <span className="font-medium break-all">{current.email}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Tên hiển thị</span>
                <span className="font-medium">{current.displayName || "—"}</span>
              </div>
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Username</span>
                {editing ? (
                  <div className="flex items-center gap-2">
                    <Input
                      value={usernameDraft}
                      onChange={(e) => setUsernameDraft(e.target.value)}
                      className="h-8 w-40"
                    />
                    <Button
                      type="button"
                      size="icon-sm"
                      variant="ghost"
                      aria-label="Lưu"
                      onClick={() => void handleSaveUsername()}
                      disabled={profile.updating || !usernameDraft.trim()}
                    >
                      <Check size={14} strokeWidth={1.5} />
                    </Button>
                    <Button
                      type="button"
                      size="icon-sm"
                      variant="ghost"
                      aria-label="Huỷ"
                      onClick={() => {
                        setUsernameDraft(current.username);
                        setEditing(false);
                      }}
                    >
                      <X size={14} strokeWidth={1.5} />
                    </Button>
                  </div>
                ) : (
                  <div className="flex items-center gap-2">
                    <span className="font-medium">{current.username || "—"}</span>
                    <Button
                      type="button"
                      size="icon-sm"
                      variant="ghost"
                      aria-label="Sửa"
                      onClick={() => {
                        setUsernameDraft(current.username);
                        setEditing(true);
                      }}
                    >
                      <Pencil size={14} strokeWidth={1.5} />
                    </Button>
                  </div>
                )}
              </div>
              <div className="flex items-center justify-between">
                <span className="text-muted-foreground">Tham gia</span>
                <span className="font-medium">{formatDate(current.createdAt)}</span>
              </div>
            </CardContent>
          </Card>
        </section>

        <Hairline />

        <section className="flex flex-col gap-3">
          <p className="text-[11px] uppercase tracking-wider text-muted-foreground">
            Google Calendar
          </p>
          <Card>
            <CardContent className="flex items-center justify-between p-4">
              {google.loading ? (
                <p className="text-[13px] text-muted-foreground">Đang kiểm tra…</p>
              ) : google.error ? (
                <InlineError message={google.error} />
              ) : google.status?.connected ? (
                <>
                  <div className="flex items-center gap-2">
                    <Calendar size={16} strokeWidth={1.5} />
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
                  </div>
                  <Button
                    type="button"
                    variant="secondary"
                    size="sm"
                    onClick={() => void google.disconnect()}
                  >
                    Ngắt kết nối
                  </Button>
                </>
              ) : (
                <>
                  <p className="text-[13px] text-muted-foreground">
                    Chưa kết nối Google Calendar.
                  </p>
                  <Button
                    type="button"
                    size="sm"
                    onClick={() => void google.connect()}
                    disabled={google.connecting}
                  >
                    Kết nối
                  </Button>
                </>
              )}
            </CardContent>
          </Card>
        </section>

        <Hairline />

        <section className="flex flex-col gap-3">
          <div className="flex items-center justify-between">
            <p className="text-[11px] uppercase tracking-wider text-muted-foreground">
              Cuộc hẹn sắp tới ({meetings.items.length})
            </p>
          </div>
          {meetings.loading ? (
            <Skeleton className="h-20 w-full" />
          ) : meetings.error ? (
            <InlineError message={meetings.error} />
          ) : meetings.items.length === 0 ? (
            <p className="rounded-md border px-3 py-6 text-center text-[13px] text-muted-foreground">
              Chưa có cuộc hẹn nào.
            </p>
          ) : (
            <ul className="flex flex-col gap-2">
              {meetings.items.map((m) => (
                <li key={m.actionId}>
                  <Card>
                    <CardContent className="space-y-1.5 p-3 text-[13px]">
                      <div className="flex items-start justify-between">
                        <p className="text-[14px] font-semibold">{m.title}</p>
                        <Badge variant="outline">{m.status}</Badge>
                      </div>
                      {m.startAt ? (
                        <p className="text-[12px] text-muted-foreground">
                          {formatDateTime(m.startAt)} · {m.durationMinutes ?? 60} phút
                        </p>
                      ) : null}
                      {m.placeName ? (
                        <p className="inline-flex items-center gap-1 text-[12px] text-muted-foreground">
                          <MapPin size={12} strokeWidth={1.5} />
                          {m.placeName}
                        </p>
                      ) : null}
                      {m.attendeeEmails.length > 0 ? (
                        <p className="inline-flex items-center gap-1 text-[12px] text-muted-foreground">
                          <Mail size={12} strokeWidth={1.5} />
                          {m.attendeeEmails.join(", ")}
                        </p>
                      ) : null}
                      {m.status === "PartiallyFailed" || !m.emailsSent ? (
                        <Button
                          type="button"
                          variant="secondary"
                          size="sm"
                          onClick={() => void retryEmails.retry(m.actionId)}
                          disabled={retryEmails.retrying}
                        >
                          {COPY.meeting.retry}
                        </Button>
                      ) : null}
                    </CardContent>
                  </Card>
                </li>
              ))}
            </ul>
          )}
        </section>

        {meetingsPast.items.length > 0 ? (
          <>
            <Hairline />
            <section className="flex flex-col gap-3">
              <p className="text-[11px] uppercase tracking-wider text-muted-foreground">
                Lịch sử ({meetingsPast.items.length})
              </p>
              <ul className="flex flex-col gap-2">
                {meetingsPast.items.slice(0, 5).map((m) => (
                  <li key={m.actionId}>
                    <Card>
                      <CardContent className="flex items-start justify-between p-3">
                        <p className="text-[14px] font-medium">{m.title}</p>
                        <Badge variant="outline">{m.status}</Badge>
                      </CardContent>
                    </Card>
                  </li>
                ))}
              </ul>
            </section>
          </>
        ) : null}

        <Hairline />

        <section className="flex flex-col gap-3">
          <p className="text-[11px] uppercase tracking-wider text-muted-foreground">
            Đánh giá của tôi ({reviews.items.length})
          </p>
          {reviews.loading ? (
            <Skeleton className="h-16 w-full" />
          ) : reviews.error ? (
            <InlineError message={reviews.error} />
          ) : reviews.items.length === 0 ? (
            <p className="rounded-md border px-3 py-6 text-center text-[13px] text-muted-foreground">
              Bạn chưa viết đánh giá nào.
            </p>
          ) : (
            <ul className="flex flex-col gap-2">
              {reviews.items.map((r) => (
                <li key={r.id}>
                  <Card>
                    <CardContent className="space-y-1 p-3 text-[13px]">
                      <div className="flex items-center justify-between">
                        <div className="inline-flex items-center gap-1 font-medium">
                          <Star
                            size={12}
                            strokeWidth={1.5}
                            fill="currentColor"
                          />
                          {r.rating}/5
                        </div>
                        <span className="text-[11px] text-muted-foreground">
                          {formatDate(r.createdAt)}
                        </span>
                      </div>
                      {r.comment ? (
                        <p className="text-muted-foreground">{r.comment}</p>
                      ) : null}
                    </CardContent>
                  </Card>
                </li>
              ))}
            </ul>
          )}
        </section>

        <Hairline />

        <section className="flex flex-col gap-3">
          <p className="text-[11px] uppercase tracking-wider text-muted-foreground">
            Đã lưu ({saved.items.length})
          </p>
          {saved.loading ? (
            <Skeleton className="h-16 w-full" />
          ) : saved.error ? (
            <InlineError message={saved.error} />
          ) : saved.items.length === 0 ? (
            <p className="rounded-md border px-3 py-6 text-center text-[13px] text-muted-foreground">
              {COPY.profile.savedPlacesEmpty}
            </p>
          ) : (
            <ul className="flex flex-col gap-2">
              {saved.items.map((p) => (
                <li key={p.savedPlaceId}>
                  <Card>
                    <CardContent className="flex items-center justify-between p-3">
                      <div>
                        <p className="text-[14px] font-medium">{p.name ?? "—"}</p>
                        {p.address ? (
                          <p className="text-[12px] text-muted-foreground">
                            {p.address}
                          </p>
                        ) : null}
                      </div>
                      <AlertDialog>
                        <AlertDialogTrigger
                          render={
                            <Button
                              type="button"
                              variant="ghost"
                              size="icon-sm"
                              aria-label="Bỏ lưu"
                            >
                              <Trash2 size={14} strokeWidth={1.5} />
                            </Button>
                          }
                        />
                        <AlertDialogContent>
                          <AlertDialogHeader>
                            <AlertDialogTitle>Bỏ lưu địa điểm này?</AlertDialogTitle>
                            <AlertDialogDescription>
                              {p.name
                                ? `Bỏ lưu "${p.name}" khỏi danh sách của bạn.`
                                : "Bỏ lưu địa điểm này."}
                            </AlertDialogDescription>
                          </AlertDialogHeader>
                          <AlertDialogFooter>
                            <AlertDialogCancel>Huỷ</AlertDialogCancel>
                            <AlertDialogAction
                              onClick={() => void saved.unsave(p.placeId)}
                            >
                              Bỏ lưu
                            </AlertDialogAction>
                          </AlertDialogFooter>
                        </AlertDialogContent>
                      </AlertDialog>
                    </CardContent>
                  </Card>
                </li>
              ))}
            </ul>
          )}
        </section>
      </div>
    </div>
  );
}
