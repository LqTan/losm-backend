"use client";

import { useCallback } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  fetchGoogleCalendarStatus,
  type GoogleCalendarStatus,
} from "@/entities/meeting";

export const GOOGLE_CALENDAR_STATUS_QUERY_KEY = [
  "google-calendar",
  "status",
] as const;

export function useGoogleCalendar() {
  const qc = useQueryClient();
  const q = useQuery({
    queryKey: [...GOOGLE_CALENDAR_STATUS_QUERY_KEY],
    queryFn: () => fetchGoogleCalendarStatus(),
    staleTime: 30_000,
  });
  const connect = useMutation({
    mutationFn: async () => {
      window.location.assign("/api/proxy/users/me/google-calendar/connect");
    },
    onError: () => toast.error("Không kết nối được Google Calendar."),
  });
  const disconnect = useMutation({
    mutationFn: async () => {
      const res = await fetch(
        "/api/proxy/users/me/google-calendar/disconnect",
        { method: "DELETE" },
      );
      if (!res.ok) throw new Error("disconnect_failed");
    },
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: [...GOOGLE_CALENDAR_STATUS_QUERY_KEY] });
      toast.success("Đã ngắt kết nối Google Calendar.");
    },
    onError: () => toast.error("Không ngắt kết nối được."),
  });

  const connectCallback = useCallback(() => {
    window.location.assign("/api/proxy/users/me/google-calendar/connect");
  }, []);

  return {
    status: q.data as GoogleCalendarStatus | undefined,
    loading: q.isPending,
    error: q.error ? "Không kiểm tra được trạng thái Google Calendar." : null,
    connecting: connect.isPending,
    connect: connectCallback,
    connectMutation: connect.mutateAsync,
    disconnect: disconnect.mutateAsync,
    reload: () =>
      qc.invalidateQueries({ queryKey: [...GOOGLE_CALENDAR_STATUS_QUERY_KEY] }),
  };
}
