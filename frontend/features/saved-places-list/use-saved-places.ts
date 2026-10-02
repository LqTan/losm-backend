"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import {
  fetchSavedPlaces,
  unsavePlace as unsavePlaceApi,
  type SavedPlace,
} from "@/entities/meeting";

export const SAVED_PLACES_LIST_QUERY_KEY = ["saved-places", "list"] as const;

export function useSavedPlaces() {
  const qc = useQueryClient();
  const list = useQuery({
    queryKey: [...SAVED_PLACES_LIST_QUERY_KEY],
    queryFn: () => fetchSavedPlaces(),
    staleTime: 60_000,
  });
  const remove = useMutation({
    mutationFn: (placeId: string) => unsavePlaceApi(placeId),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: [...SAVED_PLACES_LIST_QUERY_KEY] });
      toast.success("Đã bỏ lưu.");
    },
    onError: () => toast.error("Không bỏ lưu được."),
  });
  return {
    items: (list.data ?? []) as readonly SavedPlace[],
    loading: list.isPending,
    error: list.error ? "Không tải được danh sách đã lưu." : null,
    unsave: remove.mutateAsync,
    unsaving: remove.isPending,
    reload: () => qc.invalidateQueries({ queryKey: [...SAVED_PLACES_LIST_QUERY_KEY] }),
  };
}
