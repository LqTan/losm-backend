"use client";

import { useQuery } from "@tanstack/react-query";
import { fetchCurrentSession, type User } from "@/entities/user";

export const CURRENT_USER_QUERY_KEY = ["current-user"] as const;

export function useCurrentUser(initialUser?: User | null) {
  return useQuery({
    queryKey: [...CURRENT_USER_QUERY_KEY],
    queryFn: () => fetchCurrentSession(),
    initialData: initialUser
      ? { user: initialUser }
      : undefined,
    staleTime: 60_000,
  });
}
