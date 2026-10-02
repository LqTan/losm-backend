"use client";

import { useQuery } from "@tanstack/react-query";
import { fetchMeetings, type Meeting } from "@/entities/meeting";

export const MEETINGS_LIST_QUERY_KEY = (scope: "upcoming" | "past") =>
  ["meetings", scope] as const;

export function useMeetingsList({ scope }: { scope: "upcoming" | "past" }) {
  const q = useQuery({
    queryKey: [...MEETINGS_LIST_QUERY_KEY(scope)],
    queryFn: () => fetchMeetings(scope),
    staleTime: 60_000,
  });
  return {
    items: (q.data ?? []) as readonly Meeting[],
    loading: q.isPending,
    error: q.error ? "Không tải được danh sách cuộc hẹn." : null,
    reload: () => q.refetch(),
  };
}
