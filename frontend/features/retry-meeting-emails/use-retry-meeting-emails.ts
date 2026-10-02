"use client";

import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import { retryMeetingEmails as retryApi } from "@/entities/meeting";

export function useRetryMeetingEmails() {
  const mutation = useMutation({
    mutationFn: (actionId: string) => retryApi(actionId),
    onSuccess: () => toast.success("Đã gửi lại email."),
    onError: () => toast.error("Không gửi lại email được."),
  });
  return {
    retry: mutation.mutateAsync,
    retrying: mutation.isPending,
    error: mutation.error ? "Không gửi lại email được." : null,
  };
}
