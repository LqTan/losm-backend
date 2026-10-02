"use client";

import { useMutation } from "@tanstack/react-query";
import { toast } from "sonner";
import { createReview as createReviewApi } from "@/entities/meeting";

export interface CreateReviewVars {
  readonly placeId: string;
  readonly rating: number;
  readonly comment: string;
}

export function useCreateReview() {
  const mutation = useMutation({
    mutationFn: (input: CreateReviewVars) =>
      createReviewApi({
        placeId: input.placeId,
        rating: input.rating,
        comment: input.comment,
      }),
    onSuccess: () => toast.success("Đã gửi đánh giá."),
    onError: () => toast.error("Không gửi được đánh giá."),
  });
  return {
    submit: mutation.mutateAsync,
    submitting: mutation.isPending,
    error: mutation.error ? "Không gửi được đánh giá." : null,
  };
}
