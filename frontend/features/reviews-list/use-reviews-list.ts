"use client";

import { useQuery } from "@tanstack/react-query";
import { fetchMyReviews, type Review } from "@/entities/meeting";

export const MY_REVIEWS_QUERY_KEY = ["reviews", "me"] as const;

export function useMyReviews(enabled = true) {
  const q = useQuery({
    queryKey: [...MY_REVIEWS_QUERY_KEY],
    queryFn: () => fetchMyReviews(),
    enabled,
    staleTime: 60_000,
  });
  return {
    items: (q.data ?? []) as readonly Review[],
    loading: q.isPending,
    error: q.error ? "Không tải được đánh giá." : null,
    reload: () => q.refetch(),
  };
}
