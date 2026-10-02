"use client";

import { useMemo, useState } from "react";
import { CalendarClock, MapPin, Users } from "lucide-react";
import { useQuery } from "@tanstack/react-query";
import { fetchPendingAction, type PendingActionDetail } from "@/entities/meeting";
import { useConfirmMeeting } from "@/features/confirm-meeting";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Textarea } from "@/components/ui/textarea";
import { Skeleton } from "@/components/ui/skeleton";
import { InlineError } from "@/shared/ui/inline-error";
import { COPY } from "@/shared/config/copy";

export interface MeetingConfirmDialogProps {
  readonly actionId: string | null;
  readonly placeName: string;
  readonly onClose: () => void;
  readonly onConfirmed?: (actionId: string) => void;
}

export function MeetingConfirmDialog({
  actionId,
  placeName,
  onClose,
  onConfirmed,
}: MeetingConfirmDialogProps) {
  return (
    <Dialog
      open={actionId !== null}
      onOpenChange={(open) => {
        if (!open) onClose();
      }}
    >
      <DialogContent className="max-w-lg">
        <DialogHeader>
          <DialogTitle>{COPY.meeting.confirmTitle}</DialogTitle>
          <DialogDescription>{COPY.meeting.confirmSubtitle}</DialogDescription>
        </DialogHeader>
        {actionId ? (
          <MeetingConfirmBody
            actionId={actionId}
            placeName={placeName}
            onClose={onClose}
            {...(onConfirmed ? { onConfirmed } : {})}
          />
        ) : null}
      </DialogContent>
    </Dialog>
  );
}

const PENDING_QUERY_KEY = (actionId: string) =>
  ["agent", "pending-action", actionId] as const;

function MeetingConfirmBody({
  actionId,
  placeName,
  onClose,
  onConfirmed,
}: {
  readonly actionId: string;
  readonly placeName: string;
  readonly onClose: () => void;
  readonly onConfirmed?: (actionId: string) => void;
}) {
  const query = useQuery({
    queryKey: [...PENDING_QUERY_KEY(actionId)],
    queryFn: () => fetchPendingAction(actionId),
    enabled: Boolean(actionId),
    retry: false,
  });

  if (query.isPending) {
    return (
      <div className="flex flex-col gap-2 p-4">
        <Skeleton className="h-9 w-full" />
        <Skeleton className="h-9 w-full" />
        <Skeleton className="h-20 w-full" />
      </div>
    );
  }
  if (query.error) {
    return (
      <div className="p-4">
        <InlineError message="Không tải được chi tiết lịch hẹn." />
      </div>
    );
  }
  if (!query.data) return null;
  return (
    <MeetingConfirmForm
      detail={query.data}
      placeName={placeName}
      onClose={onClose}
      {...(onConfirmed ? { onConfirmed } : {})}
    />
  );
}

function MeetingConfirmForm({
  detail,
  placeName,
  onClose,
  onConfirmed,
}: {
  readonly detail: PendingActionDetail;
  readonly placeName: string;
  readonly onClose: () => void;
  readonly onConfirmed?: (actionId: string) => void;
}) {
  const payload = detail.payload;
  const initialStartAt = useMemo(
    () => isoToLocalInput(payload.startAt),
    [payload.startAt],
  );
  const initialAttendees = useMemo(
    () => (payload.attendees ?? []).join(", "),
    [payload.attendees],
  );

  const [title, setTitle] = useState(() => payload.title ?? placeName ?? "");
  const [startAt, setStartAt] = useState(() => initialStartAt);
  const [attendeesRaw, setAttendeesRaw] = useState(() => initialAttendees);
  const [notes, setNotes] = useState(() => payload.note ?? "");

  const { submit, submitting, error } = useConfirmMeeting({
    actionId: detail.actionId,
    placeName: placeName || payload.title || "(không có tiêu đề)",
    onSuccess: () => {
      onConfirmed?.(detail.actionId);
      onClose();
    },
  });

  const titleMissing = title.trim().length === 0;
  const startAtMissing = startAt.trim().length === 0;
  const attendeesMissing =
    attendeesRaw
      .split(",")
      .map((s) => s.trim())
      .filter((s) => s.length > 0).length === 0;
  const cannotSubmit = titleMissing || startAtMissing || attendeesMissing;

  const handleSubmit = (e: React.FormEvent<HTMLFormElement>) => {
    e.preventDefault();
    if (cannotSubmit) return;
    void submit({
      title: title.trim(),
      startAt: localInputToIso(startAt),
      attendeesRaw: attendeesRaw.trim(),
      notes: notes.trim(),
    });
  };

  const placeDisplay =
    payload.placeName ?? placeName ?? COPY.place.detail.dash;

  return (
    <form onSubmit={handleSubmit} className="flex flex-col gap-4 p-4">
      <div className="flex flex-col gap-1">
        <Label htmlFor="meeting-title">{COPY.meeting.fieldTitle}</Label>
        <Input
          id="meeting-title"
          value={title}
          onChange={(e) => setTitle(e.target.value)}
          placeholder="Cafe Coffee Day - Tâm sự cùng Thu Mỹn"
        />
      </div>

      <div className="flex flex-col gap-1">
        <Label htmlFor="meeting-start">{COPY.meeting.fieldStart}</Label>
        <Input
          id="meeting-start"
          type="datetime-local"
          value={startAt}
          onChange={(e) => setStartAt(e.target.value)}
        />
      </div>

      <div className="flex flex-col gap-1">
        <Label htmlFor="meeting-attendees">{COPY.meeting.fieldAttendees}</Label>
        <Textarea
          id="meeting-attendees"
          value={attendeesRaw}
          onChange={(e) => setAttendeesRaw(e.target.value)}
          rows={3}
          placeholder="a@example.com, b@example.com"
        />
      </div>

      <div className="flex flex-col gap-1">
        <Label htmlFor="meeting-notes">{COPY.meeting.fieldNotes}</Label>
        <Textarea
          id="meeting-notes"
          value={notes}
          onChange={(e) => setNotes(e.target.value)}
          rows={2}
          placeholder={COPY.place.detail.dash}
        />
      </div>

      <div className="flex items-start gap-2 text-[12px] text-muted-foreground">
        <MapPin
          size={14}
          strokeWidth={1.5}
          className="mt-0.5 shrink-0"
          aria-hidden
        />
        <span>{placeDisplay}</span>
      </div>
      <div className="flex items-start gap-2 text-[12px] text-muted-foreground">
        <Users
          size={14}
          strokeWidth={1.5}
          className="mt-0.5 shrink-0"
          aria-hidden
        />
        <span>{detail.description || COPY.place.detail.dash}</span>
      </div>

      {error ? <InlineError message={error} /> : null}

      <DialogFooter className="grid grid-cols-2 gap-2">
        <Button type="button" variant="secondary" onClick={onClose}>
          Đóng
        </Button>
        <Button type="submit" disabled={cannotSubmit || submitting}>
          <CalendarClock size={16} strokeWidth={1.5} />
          {COPY.meeting.submit}
        </Button>
      </DialogFooter>
    </form>
  );
}

function isoToLocalInput(iso: string | undefined): string {
  if (!iso) return "";
  const d = new Date(iso);
  if (Number.isNaN(d.getTime())) return "";
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function localInputToIso(value: string): string {
  if (!value) return "";
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return d.toISOString();
}
