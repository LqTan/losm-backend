"use client";

import { Loader2 } from "lucide-react";

export interface ChatTypingProps {
  readonly step: string | null;
}

export function ChatTyping({ step }: ChatTypingProps) {
  return (
    <div className="flex items-center gap-2 px-1 text-[12px] text-muted-foreground">
      <Loader2 size={14} strokeWidth={1.5} className="animate-spin" aria-hidden />
      <span>{step ?? "Đang soạn câu trả lời…"}</span>
    </div>
  );
}
