"use client";

import { useEffect, useRef, useState } from "react";
import { Send, Square } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Textarea } from "@/components/ui/textarea";
import { COPY } from "@/shared/config/copy";

export interface ChatInputProps {
  readonly onSend: (text: string) => Promise<void> | void;
  readonly streaming?: boolean;
  readonly onStop?: () => void;
  readonly disabled?: boolean;
  readonly placeholder?: string;
}

const EXTERNAL_INPUT_EVENT = "losm:set-chat-input";

export function ChatInput({
  onSend,
  streaming,
  onStop,
  disabled,
  placeholder,
}: ChatInputProps) {
  const [value, setValue] = useState("");
  const ref = useRef<HTMLTextAreaElement | null>(null);

  useEffect(() => {
    const el = ref.current;
    if (!el) return;
    el.style.height = "auto";
    el.style.height = `${Math.min(el.scrollHeight, 160)}px`;
  }, [value]);

  useEffect(() => {
    function handler(e: Event) {
      const detail = (e as CustomEvent<string>).detail;
      if (typeof detail !== "string") return;
      setValue(detail);
      requestAnimationFrame(() => {
        ref.current?.focus();
      });
    }
    window.addEventListener(EXTERNAL_INPUT_EVENT, handler);
    return () => window.removeEventListener(EXTERNAL_INPUT_EVENT, handler);
  }, []);

  function submit() {
    const text = value.trim();
    if (!text) return;
    void onSend(text);
    setValue("");
  }

  return (
    <div className="flex items-end gap-2 border-t bg-background px-3 py-2">
      <Textarea
        ref={ref}
        value={value}
        onChange={(e) => setValue(e.target.value)}
        placeholder={placeholder ?? COPY.chat.placeholder}
        rows={1}
        className="flex-1 resize-none border-0 bg-transparent py-2 text-[14px] leading-[1.5] shadow-none focus-visible:ring-0"
        onKeyDown={(e) => {
          if (e.key === "Enter" && !e.shiftKey) {
            e.preventDefault();
            submit();
          }
        }}
      />
      {streaming ? (
        <Button
          type="button"
          variant="secondary"
          size="icon-sm"
          aria-label="Dừng"
          onClick={() => onStop?.()}
        >
          <Square size={14} strokeWidth={1.5} />
        </Button>
      ) : (
        <Button
          type="button"
          size="icon-sm"
          aria-label="Gửi"
          onClick={submit}
          disabled={disabled || value.trim().length === 0}
        >
          <Send size={14} strokeWidth={1.5} />
        </Button>
      )}
    </div>
  );
}
