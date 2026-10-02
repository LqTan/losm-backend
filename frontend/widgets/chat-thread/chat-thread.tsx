"use client";

import { useEffect, useReducer, useRef } from "react";
import type { ChatMessage } from "@/entities/chat-session/model";
import { ChatBubble } from "./chat-bubble";
import { ChatTyping } from "./chat-typing";
import { EmptyState } from "@/shared/ui/empty-state";
import { COPY } from "@/shared/config/copy";

type State = {
  byId: Record<string, ChatMessage>;
  order: string[];
};

type Action =
  | { type: "upsert"; message: ChatMessage }
  | { type: "reset"; messages: readonly ChatMessage[] };

function reducer(state: State, action: Action): State {
  switch (action.type) {
    case "upsert": {
      const existing = state.byId[action.message.id];
      const merged: ChatMessage = existing
        ? {
            ...existing,
            ...action.message,
            ...(action.message.places
              ? { places: action.message.places }
              : {}),
          }
        : action.message;
      const nextById = { ...state.byId, [action.message.id]: merged };
      const order =
        existing ? state.order : [...state.order, action.message.id];
      return { byId: nextById, order };
    }
    case "reset": {
      const byId: Record<string, ChatMessage> = {};
      const order: string[] = [];
      for (const m of action.messages) {
        byId[m.id] = m;
        order.push(m.id);
      }
      return { byId, order };
    }
    default:
      return state;
  }
}

export interface ChatThreadProps {
  readonly messages: readonly ChatMessage[];
  readonly streaming: boolean;
  readonly latestStep?: string | null;
  readonly onSelectPlace?: (messageId: string, placeIndex: number) => void;
  readonly highlightedPlaceId?: string | null;
  readonly onConfirmAction?: (
    pendingActionId: string,
    placeName: string,
  ) => void;
}

export function ChatThread({
  messages,
  streaming,
  latestStep,
  onSelectPlace,
  highlightedPlaceId,
  onConfirmAction,
}: ChatThreadProps) {
  const [, dispatch] = useReducer(reducer, { byId: {}, order: [] });
  const listRef = useRef<HTMLDivElement | null>(null);

  useEffect(() => {
    const last = listRef.current?.lastElementChild;
    if (last && "scrollIntoView" in last) {
      (last as HTMLElement).scrollIntoView({ block: "end" });
    }
  }, [messages.length, streaming]);

  useEffect(() => {
    dispatch({ type: "reset", messages });
  }, [messages]);

  if (messages.length === 0) {
    return <EmptyState title={COPY.chat.empty} />;
  }

  return (
    <div ref={listRef} className="flex flex-col gap-3 py-4">
      {messages.map((msg) => (
        <ChatBubble
          key={msg.id}
          message={msg}
          {...(onSelectPlace
            ? {
                onSelectPlace: (idx: number) => onSelectPlace(msg.id, idx),
              }
            : {})}
          {...(highlightedPlaceId !== undefined
            ? { highlightedPlaceId }
            : {})}
          {...(onConfirmAction ? { onConfirmAction } : {})}
        />
      ))}
      {streaming ? (
        <ChatTyping
          {...(latestStep !== undefined ? { step: latestStep } : { step: null })}
        />
      ) : null}
    </div>
  );
}

export function upsertMessage(
  current: readonly ChatMessage[],
  next: ChatMessage,
): readonly ChatMessage[] {
  const idx = current.findIndex((m) => m.id === next.id);
  if (idx === -1) return [...current, next];
  const merged: ChatMessage = {
    ...current[idx]!,
    ...next,
    ...(next.places ? { places: next.places } : {}),
  };
  const copy = current.slice();
  copy[idx] = merged;
  return copy;
}
