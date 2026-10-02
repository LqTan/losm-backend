"use client";

import { useQuery } from "@tanstack/react-query";
import { fetchSessionList, type ChatSession } from "@/entities/chat-session";

export const SESSION_LIST_QUERY_KEY = ["chat-sessions", "list"] as const;

export interface UseSessionListResult {
  readonly sessions: readonly ChatSession[];
  readonly loading: boolean;
  readonly error: string | null;
  readonly reload: () => void;
}

export function useSessionList(): UseSessionListResult {
  const q = useQuery({
    queryKey: [...SESSION_LIST_QUERY_KEY],
    queryFn: () => fetchSessionList(),
    staleTime: 30_000,
  });
  return {
    sessions: (q.data ?? []) as readonly ChatSession[],
    loading: q.isPending,
    error: q.error ? "Không tải được danh sách phiên." : null,
    reload: () => q.refetch(),
  };
}
