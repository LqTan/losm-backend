"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { savePlace as savePlaceApi } from "@/entities/meeting";
import { SAVED_PLACES_LIST_QUERY_KEY } from "@/features/saved-places-list/use-saved-places";

export function useSavePlace() {
  const qc = useQueryClient();
  const mutation = useMutation({
    mutationFn: (placeId: string) => savePlaceApi(placeId),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: [...SAVED_PLACES_LIST_QUERY_KEY] });
    },
    onError: () => toast.error("Không lưu được địa điểm."),
  });
  return {
    save: mutation.mutateAsync,
    saving: mutation.isPending,
    error: mutation.error ? "Không lưu được địa điểm." : null,
  };
}
