"use client";

import { useMutation } from "@tanstack/react-query";
import { confirmMeeting as confirmMeetingApi } from "@/entities/meeting";

export interface ConfirmMeetingVars {
  readonly title: string;
  readonly startAt: string;
  readonly attendeesRaw: string;
  readonly notes: string;
}

export interface UseConfirmMeetingArgs {
  readonly actionId: string;
  readonly placeName: string;
  readonly onSuccess?: () => void;
}

export function useConfirmMeeting({ actionId, onSuccess }: UseConfirmMeetingArgs) {
  const mutation = useMutation({
    mutationFn: (vars: ConfirmMeetingVars) =>
      confirmMeetingApi({
        actionId,
        title: vars.title,
        startAt: vars.startAt,
        attendeesRaw: vars.attendeesRaw,
        notes: vars.notes,
      }),
    onSuccess: () => {
      onSuccess?.();
    },
  });
  return {
    submit: mutation.mutateAsync,
    submitting: mutation.isPending,
    error: mutation.error
      ? "Không xác nhận được lịch hẹn. Vui lòng kiểm tra email và thử lại."
      : null,
  };
}
