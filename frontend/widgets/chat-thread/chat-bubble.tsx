"use client";

import { CalendarClock } from "lucide-react";
import type { AttachedPlace, ChatMessage } from "@/entities/chat-session/model";
import { Card } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { COPY } from "@/shared/config/copy";
import { cn } from "@/shared/lib/cn";

export interface ChatBubbleProps {
  readonly message: ChatMessage;
  readonly onSelectPlace?: (index: number) => void;
  readonly highlightedPlaceId?: string | null;
  readonly onConfirmAction?: (
    pendingActionId: string,
    placeName: string,
  ) => void;
}

export function ChatBubble({
  message,
  onSelectPlace,
  highlightedPlaceId,
  onConfirmAction,
}: ChatBubbleProps) {
  const isUser = message.role === "user";
  const placeName =
    message.places && message.places.length > 0
      ? (message.places[0]?.name ?? "")
      : "";

  return (
    <div
      className={cn(
        "flex flex-col gap-1",
        isUser ? "items-end" : "items-start",
      )}
    >
      <span className="px-1 text-[11px] text-muted-foreground">
        {isUser ? COPY.chat.role.user : COPY.chat.role.assistant}
      </span>
      <div
        className={cn(
          "max-w-[85%] rounded-md px-3 py-2 text-[14px] leading-[1.5]",
          isUser
            ? "bg-primary text-primary-foreground"
            : "border bg-card text-card-foreground",
        )}
      >
        {message.content || (
          <span
            aria-hidden
            className="inline-flex gap-1 leading-none text-muted-foreground"
          >
            <span className="chat-dot">·</span>
            <span className="chat-dot">·</span>
            <span className="chat-dot">·</span>
          </span>
        )}
      </div>
      {message.places && message.places.length > 0 ? (
        <ul className="mt-1 flex w-full max-w-[85%] flex-col gap-2">
          {message.places.map((p, i) => (
            <li key={p.id}>
              <button
                type="button"
                onClick={() => onSelectPlace?.(i)}
                className="w-full text-left"
                aria-label={`Xem ${p.name}`}
              >
                <Card
                  className={cn(
                    "p-3 transition-colors",
                    highlightedPlaceId === p.id && "ring-2 ring-foreground/30",
                  )}
                >
                  <p className="text-[14px] font-semibold line-clamp-1">
                    {p.name}
                  </p>
                  <p className="mt-0.5 text-[12px] text-muted-foreground line-clamp-1">
                    {p.address ?? "—"}
                  </p>
                  {typeof p.distanceKm === "number" ? (
                    <p className="mt-1 text-[12px]">
                      {(p.distanceKm ?? 0).toFixed(2)} km
                    </p>
                  ) : null}
                </Card>
              </button>
            </li>
          ))}
        </ul>
      ) : null}
      {message.pendingActionId ? (
        <div className="mt-1 max-w-[85%]">
          <Button
            type="button"
            variant="secondary"
            size="sm"
            onClick={() =>
              onConfirmAction?.(message.pendingActionId ?? "", placeName)
            }
          >
            <CalendarClock size={14} strokeWidth={1.5} />
            Xác nhận lịch hẹn
          </Button>
        </div>
      ) : null}
    </div>
  );
}

export function toAttachedPlace(p: AttachedPlace) {
  return p;
}
