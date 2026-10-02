"use client";

import { useMutation, useQueryClient } from "@tanstack/react-query";
import { toast } from "sonner";
import { updateProfile as updateProfileApi, type User } from "@/entities/user";
import { CURRENT_USER_QUERY_KEY } from "@/features/current-user/use-current-user";

export function useUpdateProfile() {
  const qc = useQueryClient();
  const mutation = useMutation({
    mutationFn: (username: string) => updateProfileApi({ username }),
    onSuccess: (data: User) => {
      void qc.invalidateQueries({ queryKey: [...CURRENT_USER_QUERY_KEY] });
      toast.success("Đã cập nhật hồ sơ.");
      return data;
    },
    onError: () => toast.error("Không cập nhật được hồ sơ."),
  });
  return {
    update: mutation.mutateAsync,
    updating: mutation.isPending,
    error: mutation.error ? "Không cập nhật được hồ sơ." : null,
  };
}
