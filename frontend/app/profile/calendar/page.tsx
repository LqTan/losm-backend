"use client";

import { useEffect, useState } from "react";
import { api } from "@/lib/api";
import { getToken } from "@/lib/auth";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  Calendar as CalendarIcon,
  ChevronLeft,
  ChevronRight,
  Clock,
  ExternalLink,
  List,
  Loader2,
  MapPin,
  Plus,
  Users,
} from "lucide-react";
import Link from "next/link";

interface MeetingItem {
  id: string;
  title: string;
  placeName?: string;
  address?: string;
  startAt?: string;
  durationMinutes?: number;
  attendees?: string[];
  note?: string;
  status: string;
  emailsSent?: boolean;
}

const WEEKDAYS = ["T2", "T3", "T4", "T5", "T6", "T7", "CN"];

export default function ProfileCalendarPage() {
  const [meetings, setMeetings] = useState<MeetingItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [currentDate, setCurrentDate] = useState(() => new Date());
  const [viewMode, setViewMode] = useState<"month" | "week" | "agenda">("month");
  const [selectedMeeting, setSelectedMeeting] = useState<MeetingItem | null>(null);

  useEffect(() => {
    async function fetchMeetings() {
      const token = getToken();
      if (!token) {
        setLoading(false);
        return;
      }

      try {
        const data = await api.get<any[]>("/api/meetings", token);
        if (Array.isArray(data)) {
          const mapped: MeetingItem[] = data.map((item) => ({
            id: item.id || `m_${Math.random()}`,
            title: item.title || item.placeName || "Cuộc hẹn",
            placeName: item.placeName,
            address: item.address,
            startAt: item.startAt,
            durationMinutes: item.durationMinutes || 60,
            attendees: item.attendees || [],
            note: item.note,
            status: item.status || "Completed",
            emailsSent: item.emailsSent,
          }));
          setMeetings(mapped);
        }
      } catch {
        setMeetings([]);
      } finally {
        setLoading(false);
      }
    }

    void fetchMeetings();
  }, []);

  // Calendar calculations
  const year = currentDate.getFullYear();
  const month = currentDate.getMonth();

  function prevMonth() {
    setCurrentDate(new Date(year, month - 1, 1));
  }

  function nextMonth() {
    setCurrentDate(new Date(year, month + 1, 1));
  }

  function goToToday() {
    setCurrentDate(new Date());
  }

  // Get calendar cells for current month
  const firstDayOfMonth = new Date(year, month, 1);
  const lastDayOfMonth = new Date(year, month + 1, 0);

  // Day of week index 0-6 starting Monday (0 = Monday, 6 = Sunday)
  let startDayOfWeek = firstDayOfMonth.getDay() - 1;
  if (startDayOfWeek === -1) startDayOfWeek = 6;

  const totalDays = lastDayOfMonth.getDate();
  const prevMonthLastDay = new Date(year, month, 0).getDate();

  const days: Array<{
    dayNumber: number;
    isCurrentMonth: boolean;
    date: Date;
    meetings: MeetingItem[];
  }> = [];

  // Previous month trailing days
  for (let i = startDayOfWeek - 1; i >= 0; i--) {
    const d = new Date(year, month - 1, prevMonthLastDay - i);
    days.push({
      dayNumber: prevMonthLastDay - i,
      isCurrentMonth: false,
      date: d,
      meetings: getMeetingsForDate(d),
    });
  }

  // Current month days
  for (let i = 1; i <= totalDays; i++) {
    const d = new Date(year, month, i);
    days.push({
      dayNumber: i,
      isCurrentMonth: true,
      date: d,
      meetings: getMeetingsForDate(d),
    });
  }

  // Next month leading days (fill remaining of 35 or 42 cells)
  const remaining = 7 - (days.length % 7);
  if (remaining < 7) {
    for (let i = 1; i <= remaining; i++) {
      const d = new Date(year, month + 1, i);
      days.push({
        dayNumber: i,
        isCurrentMonth: false,
        date: d,
        meetings: getMeetingsForDate(d),
      });
    }
  }

  function getMeetingsForDate(targetDate: Date): MeetingItem[] {
    return meetings.filter((m) => {
      if (!m.startAt) return false;
      const mDate = new Date(m.startAt);
      return (
        mDate.getFullYear() === targetDate.getFullYear() &&
        mDate.getMonth() === targetDate.getMonth() &&
        mDate.getDate() === targetDate.getDate()
      );
    });
  }

  const today = new Date();
  const isTodayDate = (d: Date) =>
    d.getFullYear() === today.getFullYear() &&
    d.getMonth() === today.getMonth() &&
    d.getDate() === today.getDate();

  const monthNames = [
    "Tháng 1",
    "Tháng 2",
    "Tháng 3",
    "Tháng 4",
    "Tháng 5",
    "Tháng 6",
    "Tháng 7",
    "Tháng 8",
    "Tháng 9",
    "Tháng 10",
    "Tháng 11",
    "Tháng 12",
  ];

  return (
    <div className="space-y-4">
      {/* Top Google Calendar Toolbar */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3 border-b bg-card p-4 rounded-xl border shadow-sm">
        <div className="flex items-center gap-2.5 flex-wrap">
          <Button
            variant="outline"
            size="sm"
            onClick={goToToday}
            className="text-xs h-8 font-medium px-3"
          >
            Hôm nay
          </Button>

          <div className="flex items-center gap-1">
            <Button
              variant="ghost"
              size="icon"
              onClick={prevMonth}
              className="h-8 w-8 text-muted-foreground hover:text-foreground"
              title="Tháng trước"
            >
              <ChevronLeft className="h-4 w-4" />
            </Button>
            <Button
              variant="ghost"
              size="icon"
              onClick={nextMonth}
              className="h-8 w-8 text-muted-foreground hover:text-foreground"
              title="Tháng sau"
            >
              <ChevronRight className="h-4 w-4" />
            </Button>
          </div>

          <h2 className="text-base sm:text-lg font-bold text-foreground">
            {monthNames[month]}, {year}
          </h2>
        </div>

        {/* View Switcher & Action */}
        <div className="flex items-center gap-2 w-full sm:w-auto justify-between sm:justify-end">
          <div className="flex items-center rounded-lg border bg-muted/40 p-0.5 text-xs">
            <button
              type="button"
              onClick={() => setViewMode("month")}
              className={`rounded-md px-2.5 py-1 font-medium transition ${
                viewMode === "month"
                  ? "bg-card text-foreground shadow-xs"
                  : "text-muted-foreground hover:text-foreground"
              }`}
            >
              Tháng
            </button>
            <button
              type="button"
              onClick={() => setViewMode("agenda")}
              className={`rounded-md px-2.5 py-1 font-medium transition flex items-center gap-1 ${
                viewMode === "agenda"
                  ? "bg-card text-foreground shadow-xs"
                  : "text-muted-foreground hover:text-foreground"
              }`}
            >
              <List className="h-3 w-3" />
              <span>Danh sách</span>
            </button>
          </div>

          <Link href="/">
            <Button size="sm" className="h-8 text-xs gap-1.5 font-medium">
              <Plus className="h-3.5 w-3.5" />
              <span>Hẹn gặp mới</span>
            </Button>
          </Link>
        </div>
      </div>

      {loading ? (
        <div className="flex h-96 items-center justify-center rounded-xl border bg-card">
          <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
        </div>
      ) : viewMode === "month" ? (
        /* Month Grid (Google Calendar style) */
        <div className="rounded-xl border bg-card shadow-sm overflow-hidden">
          {/* Weekday Header */}
          <div className="grid grid-cols-7 border-b bg-muted/30 text-center text-xs font-semibold text-muted-foreground">
            {WEEKDAYS.map((wd, idx) => (
              <div key={idx} className="py-2.5">
                {wd}
              </div>
            ))}
          </div>

          {/* Days Grid */}
          <div className="grid grid-cols-7 divide-x divide-y divide-border/60">
            {days.map((item, idx) => {
              const isToday = isTodayDate(item.date);
              return (
                <div
                  key={idx}
                  className={`min-h-[96px] sm:min-h-[110px] p-1.5 transition flex flex-col justify-between ${
                    item.isCurrentMonth
                      ? "bg-card hover:bg-muted/10"
                      : "bg-muted/15 text-muted-foreground/50"
                  }`}
                >
                  <div className="flex items-center justify-between">
                    <span
                      className={`flex h-6 w-6 items-center justify-center rounded-full text-xs font-medium ${
                        isToday
                          ? "bg-primary text-primary-foreground font-bold shadow-xs"
                          : item.isCurrentMonth
                          ? "text-foreground"
                          : "text-muted-foreground/60"
                      }`}
                    >
                      {item.dayNumber}
                    </span>

                    {item.meetings.length > 0 && (
                      <span className="text-[10px] text-muted-foreground font-mono">
                        {item.meetings.length} hẹn
                      </span>
                    )}
                  </div>

                  {/* Meeting Pills */}
                  <div className="mt-1 space-y-1 overflow-y-auto max-h-[70px] no-scrollbar">
                    {item.meetings.map((m) => {
                      const timeStr = m.startAt
                        ? new Date(m.startAt).toLocaleTimeString("vi-VN", {
                            hour: "2-digit",
                            minute: "2-digit",
                          })
                        : "";
                      return (
                        <button
                          key={m.id}
                          type="button"
                          onClick={() => setSelectedMeeting(m)}
                          className="w-full text-left truncate rounded-md bg-primary/10 hover:bg-primary/20 border border-primary/20 px-1.5 py-0.5 text-[11px] font-medium text-foreground transition"
                        >
                          <span className="font-semibold text-primary">{timeStr}</span>{" "}
                          <span>{m.title}</span>
                        </button>
                      );
                    })}
                  </div>
                </div>
              );
            })}
          </div>
        </div>
      ) : (
        /* Agenda / List View */
        <Card className="shadow-sm border bg-card">
          <CardHeader className="pb-3 border-b">
            <CardTitle className="text-sm font-semibold flex items-center justify-between">
              <span>Danh sách các cuộc hẹn ({meetings.length})</span>
            </CardTitle>
          </CardHeader>
          <CardContent className="p-0 divide-y divide-border/60">
            {meetings.length === 0 ? (
              <div className="flex flex-col items-center justify-center p-12 text-center text-muted-foreground">
                <CalendarIcon className="h-8 w-8 mb-2 text-muted-foreground/60" />
                <p className="text-sm font-medium">Chưa có lịch hẹn nào</p>
                <p className="text-xs text-muted-foreground mt-1">
                  Chọn địa điểm trên bản đồ và nhấn "Hẹn bạn bè tại đây" để tạo lịch.
                </p>
              </div>
            ) : (
              meetings.map((m) => {
                const dateObj = m.startAt ? new Date(m.startAt) : null;
                const formattedDate = dateObj
                  ? dateObj.toLocaleDateString("vi-VN", {
                      weekday: "long",
                      year: "numeric",
                      month: "long",
                      day: "numeric",
                    })
                  : "N/A";
                const formattedTime = dateObj
                  ? dateObj.toLocaleTimeString("vi-VN", {
                      hour: "2-digit",
                      minute: "2-digit",
                    })
                  : "N/A";

                return (
                  <div
                    key={m.id}
                    onClick={() => setSelectedMeeting(m)}
                    className="p-4 hover:bg-muted/20 transition cursor-pointer flex flex-col sm:flex-row items-start sm:items-center justify-between gap-3"
                  >
                    <div className="space-y-1 min-w-0">
                      <div className="flex items-center gap-2 flex-wrap">
                        <h3 className="text-sm font-semibold text-foreground truncate">
                          {m.title}
                        </h3>
                      </div>

                      <div className="flex items-center gap-3 text-xs text-muted-foreground flex-wrap">
                        <span className="flex items-center gap-1 font-medium text-foreground">
                          <Clock className="h-3.5 w-3.5 text-muted-foreground" />
                          {formattedTime} · {formattedDate}
                        </span>

                        {m.placeName && (
                          <span className="flex items-center gap-1">
                            <MapPin className="h-3.5 w-3.5 text-muted-foreground" />
                            <strong className="text-foreground">{m.placeName}</strong>
                          </span>
                        )}
                      </div>

                      {m.address && (
                        <p className="text-xs text-muted-foreground line-clamp-1">
                          {m.address}
                        </p>
                      )}
                    </div>

                    <div className="flex items-center gap-2 shrink-0">
                      {m.attendees && m.attendees.length > 0 && (
                        <span className="flex items-center gap-1 text-xs text-muted-foreground bg-muted/50 px-2 py-1 rounded-md border">
                          <Users className="h-3.5 w-3.5 text-muted-foreground" />
                          {m.attendees.length} người
                        </span>
                      )}
                      <Button variant="outline" size="sm" className="h-8 text-xs">
                        Chi tiết
                      </Button>
                    </div>
                  </div>
                );
              })
            )}
          </CardContent>
        </Card>
      )}

      {/* Meeting Detail Modal */}
      {selectedMeeting && (
        <Dialog open={!!selectedMeeting} onOpenChange={() => setSelectedMeeting(null)}>
          <DialogContent className="max-w-md p-5">
            <DialogHeader className="pb-3 border-b">
              <DialogTitle className="text-base font-bold text-foreground">
                {selectedMeeting.title}
              </DialogTitle>
            </DialogHeader>

            <div className="space-y-3.5 text-xs pt-1">
              {/* Time */}
              <div className="flex items-start gap-2.5">
                <Clock className="h-4 w-4 text-muted-foreground shrink-0 mt-0.5" />
                <div>
                  <p className="font-semibold text-foreground">Thời gian diễn ra</p>
                  <p className="text-muted-foreground mt-0.5">
                    {selectedMeeting.startAt
                      ? new Date(selectedMeeting.startAt).toLocaleString("vi-VN", {
                          dateStyle: "full",
                          timeStyle: "short",
                        })
                      : "N/A"}
                  </p>
                </div>
              </div>

              {/* Place */}
              {selectedMeeting.placeName && (
                <div className="flex items-start gap-2.5">
                  <MapPin className="h-4 w-4 text-muted-foreground shrink-0 mt-0.5" />
                  <div className="min-w-0">
                    <p className="font-semibold text-foreground">
                      {selectedMeeting.placeName}
                    </p>
                    {selectedMeeting.address && (
                      <p className="text-muted-foreground mt-0.5">
                        {selectedMeeting.address}
                      </p>
                    )}
                  </div>
                </div>
              )}

              {/* Attendees */}
              {selectedMeeting.attendees && selectedMeeting.attendees.length > 0 && (
                <div className="flex items-start gap-2.5">
                  <Users className="h-4 w-4 text-muted-foreground shrink-0 mt-0.5" />
                  <div className="w-full">
                    <p className="font-semibold text-foreground">
                      Người tham gia ({selectedMeeting.attendees.length})
                    </p>
                    <div className="mt-1 space-y-1">
                      {selectedMeeting.attendees.map((email, idx) => (
                        <div
                          key={idx}
                          className="flex items-center justify-between rounded-md bg-muted/40 px-2 py-1 text-xs"
                        >
                          <span className="truncate">{email}</span>
                        </div>
                      ))}
                    </div>
                  </div>
                </div>
              )}

              {/* Note */}
              {selectedMeeting.note && (
                <div className="rounded-lg bg-muted/30 p-2.5 border text-xs">
                  <span className="font-semibold text-foreground">Ghi chú:</span>{" "}
                  <span className="text-muted-foreground">{selectedMeeting.note}</span>
                </div>
              )}

              {/* Actions */}
              <div className="pt-2 flex items-center justify-end gap-2">
                {selectedMeeting.address && (
                  <a
                    href={`https://www.google.com/maps/search/?api=1&query=${encodeURIComponent(
                      `${selectedMeeting.placeName ?? ""} ${selectedMeeting.address}`,
                    )}`}
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    <Button variant="outline" size="sm" className="h-8 text-xs gap-1.5">
                      <ExternalLink className="h-3.5 w-3.5 text-muted-foreground" />
                      <span>Google Maps</span>
                    </Button>
                  </a>
                )}
                <Button
                  size="sm"
                  onClick={() => setSelectedMeeting(null)}
                  className="h-8 text-xs"
                >
                  Đóng
                </Button>
              </div>
            </div>
          </DialogContent>
        </Dialog>
      )}
    </div>
  );
}
