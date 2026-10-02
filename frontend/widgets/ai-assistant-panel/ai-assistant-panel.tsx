"use client";

import { Bot, Maximize2, X } from "lucide-react";
import {
  Sheet,
  SheetContent,
  SheetDescription,
  SheetHeader,
  SheetTitle,
} from "@/components/ui/sheet";
import { Button } from "@/components/ui/button";
import { useRouter } from "next/navigation";
import { ChatThread } from "@/widgets/chat-thread/chat-thread";
import { ChatInput } from "@/widgets/chat-input/chat-input";
import type { ChatMessage } from "@/entities/chat-session/model";
import { COPY } from "@/shared/config/copy";

export interface AiAssistantPanelProps {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly messages: readonly ChatMessage[];
  readonly streaming: boolean;
  readonly latestStep?: string | null;
  readonly error: string | null;
  readonly onSend: (text: string) => Promise<void> | void;
  readonly onStop: () => void;
  readonly onSelectPlace?: (messageId: string, placeIndex: number) => void;
  readonly onConfirmAction?: (actionId: string, placeName: string) => void;
}

export function AiAssistantPanel({
  open,
  onOpenChange,
  messages,
  streaming,
  latestStep,
  error,
  onSend,
  onStop,
  onSelectPlace,
  onConfirmAction,
}: AiAssistantPanelProps) {
  const router = useRouter();
  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent
        side="right"
        className="flex w-full flex-col gap-0 p-0 sm:max-w-xl"
        showCloseButton={false}
      >
        <SheetHeader className="flex-row items-center justify-between border-b px-4 py-3">
          <div className="flex items-center gap-2">
            <Bot size={18} strokeWidth={1.5} aria-hidden />
            <SheetTitle className="text-base">{COPY.search.aiAssistant}</SheetTitle>
          </div>
          <div className="flex items-center gap-1">
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              aria-label="Mở rộng"
              onClick={() => {
                onOpenChange(false);
                router.push("/assistant");
              }}
            >
              <Maximize2 size={14} strokeWidth={1.5} />
            </Button>
            <Button
              type="button"
              variant="ghost"
              size="icon-sm"
              aria-label="Đóng"
              onClick={() => onOpenChange(false)}
            >
              <X size={14} strokeWidth={1.5} />
            </Button>
          </div>
        </SheetHeader>
        <SheetDescription className="sr-only">
          {COPY.search.aiAssistantHint}
        </SheetDescription>
        <div className="flex-1 overflow-auto px-4">
          {error ? (
            <div className="py-3 text-[13px] text-destructive">{error}</div>
          ) : null}
          <ChatThread
            messages={messages}
            streaming={streaming}
            {...(latestStep !== undefined ? { latestStep } : {})}
            {...(onSelectPlace ? { onSelectPlace } : {})}
            {...(onConfirmAction ? { onConfirmAction } : {})}
          />
        </div>
        <ChatInput
          onSend={onSend}
          streaming={streaming}
          onStop={onStop}
        />
      </SheetContent>
    </Sheet>
  );
}
