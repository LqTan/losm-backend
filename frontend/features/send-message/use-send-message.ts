"use client";

import { useCallback, useRef, useState } from "react";
import { asSessionId, type SessionId } from "@/shared/lib/brands";
import type {
  AgentStreamEvent,
  AttachedPlace,
  ChatMessage,
} from "@/entities/chat-session";
import { SESSION_STORAGE_KEY } from "@/shared/config/constants";
import type { GeoPoint } from "@/entities/place/model";

export interface UseSendMessageArgs {
  readonly sessionId: SessionId | null;
  readonly onSession: (id: SessionId) => void;
  readonly onEvent: (msg: ChatMessage) => void;
  readonly location: GeoPoint;
}

export interface UseSendMessageResult {
  readonly streaming: boolean;
  readonly error: string | null;
  readonly latestStep: string | null;
  readonly send: (text: string) => Promise<void>;
  readonly stop: () => void;
}

export function useSendMessage({
  sessionId,
  onSession,
  onEvent,
  location,
}: UseSendMessageArgs): UseSendMessageResult {
  const [streaming, setStreaming] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [latestStep, setLatestStep] = useState<string | null>(null);
  const abortRef = useRef<AbortController | null>(null);

  const send = useCallback(
    async (text: string) => {
      setStreaming(true);
      setError(null);
      setLatestStep(null);
      const ctrl = new AbortController();
      abortRef.current = ctrl;
      try {
        const res = await fetch("/api/proxy/agent/stream", {
          method: "POST",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({
            message: text,
            sessionId: sessionId ?? undefined,
            latitude: location.lat,
            longitude: location.lon,
          }),
          signal: ctrl.signal,
        });
        if (!res.ok || !res.body) {
          throw new Error(`API ${res.status}`);
        }
        const reader = res.body.getReader();
        const decoder = new TextDecoder();
        let buf = "";
        for (;;) {
          const { value, done } = await reader.read();
          if (done) break;
          buf += decoder.decode(value, { stream: true });
          let idx;
          while ((idx = buf.indexOf("\n")) >= 0) {
            const line = buf.slice(0, idx).trim();
            buf = buf.slice(idx + 1);
            if (!line) continue;
            try {
              const evt = JSON.parse(line) as AgentStreamEvent;
              handleEvent(evt, onSession, onEvent, setLatestStep);
            } catch {
              /* ignore parse errors */
            }
          }
        }
        if (buf.trim()) {
          try {
            const evt = JSON.parse(buf) as AgentStreamEvent;
            handleEvent(evt, onSession, onEvent, setLatestStep);
          } catch {
            /* ignore */
          }
        }
      } catch (err) {
        if ((err as { name?: string }).name !== "AbortError") {
          setError("Mất kết nối với trợ lý. Vui lòng thử lại.");
        }
      } finally {
        setStreaming(false);
        abortRef.current = null;
        if (typeof window !== "undefined" && sessionId) {
          window.localStorage.setItem(SESSION_STORAGE_KEY, sessionId);
        }
      }
    },
    [sessionId, location.lat, location.lon, onEvent, onSession],
  );

  const stop = useCallback(() => {
    abortRef.current?.abort();
    setStreaming(false);
  }, []);

  return { streaming, error, latestStep, send, stop };
}

function handleEvent(
  evt: AgentStreamEvent,
  onSession: (id: SessionId) => void,
  onEvent: (msg: ChatMessage) => void,
  setLatestStep: (s: string | null) => void,
): void {
  if (evt.type === "step") {
    const summary =
      evt.summary ??
      (evt.kind === "ToolCall" && evt.toolName ? `Gọi ${evt.toolName}…` : null);
    if (summary) setLatestStep(summary);
    return;
  }
  if (evt.type === "error") {
    setLatestStep(null);
    return;
  }
  if (evt.type === "result") {
    onSession(asSessionId(evt.sessionId));
    const places = evt.places as readonly AttachedPlace[];
    onEvent({
      id: `assistant-${Date.now()}`,
      role: "assistant",
      content: evt.answer,
      ...(places.length ? { places } : {}),
      ...(evt.pendingActionIds[0] ? { pendingActionId: evt.pendingActionIds[0] } : {}),
      createdAt: new Date().toISOString(),
    });
    setLatestStep(null);
  }
}
