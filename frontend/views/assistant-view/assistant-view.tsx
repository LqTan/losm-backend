"use client";

import { ArrowLeft, Bot } from "lucide-react";
import { useRouter } from "next/navigation";
import { Button } from "@/components/ui/button";
import { ChatThread, upsertMessage } from "@/widgets/chat-thread";
import { ChatInput } from "@/widgets/chat-input";
import { useCallback, useEffect, useState } from "react";
import { useSendMessage } from "@/features/send-message/use-send-message";
import {
  asSessionId,
  type SessionId,
} from "@/shared/lib/brands";
import { SESSION_STORAGE_KEY } from "@/shared/config/constants";
import { features } from "@/shared/config/features";
import { fetchSessionHistory } from "@/entities/chat-session";
import type { ChatMessage } from "@/entities/chat-session/model";
import { useSessionList } from "@/features/session-list/use-session-list";
import { Skeleton } from "@/components/ui/skeleton";
import { COPY } from "@/shared/config/copy";

export interface AssistantViewProps {
  readonly initialSession?: string;
  readonly displayName?: string;
}

export function AssistantView({ initialSession, displayName }: AssistantViewProps) {
  const router = useRouter();
  const [sessionId, setSessionId] = useState<SessionId | null>(
    initialSession ? asSessionId(initialSession) : null,
  );
  const [messages, setMessages] = useState<readonly ChatMessage[]>([]);
  const [loadingHistory, setLoadingHistory] = useState(Boolean(initialSession));
  const sessionList = useSessionList();

  useEffect(() => {
    if (typeof window === "undefined") return;
    const stored = window.localStorage.getItem(SESSION_STORAGE_KEY);
    if (stored) setSessionId(asSessionId(stored));
  }, []);

  useEffect(() => {
    if (!initialSession) return;
    let cancelled = false;
    void (async () => {
      const history = await fetchSessionHistory(initialSession);
      if (cancelled) return;
      if (history) setMessages(history.messages);
      setLoadingHistory(false);
    })();
    return () => {
      cancelled = true;
    };
  }, [initialSession]);

  const handleSession = useCallback((id: SessionId) => {
    setSessionId(id);
    if (typeof window !== "undefined") {
      window.localStorage.setItem(SESSION_STORAGE_KEY, id);
    }
  }, []);

  const handleEvent = useCallback((msg: ChatMessage) => {
    setMessages((prev) => upsertMessage(prev, msg));
  }, []);

  const { streaming, error, latestStep, send, stop } = useSendMessage({
    sessionId,
    onSession: handleSession,
    onEvent: handleEvent,
    location: {
      lat: features.defaultLat,
      lon: features.defaultLon,
    },
  });

  const handleNewChat = useCallback(() => {
    setMessages([]);
    setSessionId(null);
    if (typeof window !== "undefined") {
      window.localStorage.removeItem(SESSION_STORAGE_KEY);
    }
  }, []);

  const handleSelectSession = useCallback(async (id: string) => {
    handleSession(asSessionId(id));
    const history = await fetchSessionHistory(id);
    if (history) setMessages(history.messages);
  }, [handleSession]);

  return (
    <div className="flex h-dvh flex-col">
      <header className="flex h-12 items-center justify-between border-b px-4">
        <div className="flex items-center gap-2">
          <Button
            type="button"
            variant="ghost"
            size="icon-sm"
            aria-label="Về trang chính"
            onClick={() => router.push("/")}
          >
            <ArrowLeft size={16} strokeWidth={1.5} />
          </Button>
          <Bot size={16} strokeWidth={1.5} aria-hidden />
          <p className="text-[14px] font-semibold">{COPY.search.aiAssistant}</p>
          {displayName ? (
            <span className="text-[12px] text-muted-foreground">· {displayName}</span>
          ) : null}
        </div>
        <Button type="button" variant="secondary" size="sm" onClick={handleNewChat}>
          {COPY.sessions.newChat}
        </Button>
      </header>

      <div className="flex flex-1 min-h-0">
        <aside className="hidden w-64 shrink-0 flex-col border-r lg:flex">
          <div className="flex-1 overflow-auto p-2">
            {sessionList.loading ? (
              <div className="flex flex-col gap-2">
                <Skeleton className="h-8 w-full" />
                <Skeleton className="h-8 w-3/4" />
                <Skeleton className="h-8 w-5/6" />
              </div>
            ) : sessionList.sessions.length === 0 ? (
              <p className="px-2 py-3 text-center text-[12px] text-muted-foreground">
                {COPY.sessions.empty}
              </p>
            ) : (
              <ul className="flex flex-col gap-1">
                {sessionList.sessions.map((s) => (
                  <li key={s.id}>
                    <button
                      type="button"
                      onClick={() => void handleSelectSession(s.id)}
                      className="w-full truncate rounded-md px-3 py-2 text-left text-[13px] hover:bg-muted"
                    >
                      {s.title}
                    </button>
                  </li>
                ))}
              </ul>
            )}
          </div>
        </aside>
        <section className="flex min-w-0 flex-1 flex-col">
          <div className="flex-1 overflow-auto px-4">
            {loadingHistory ? (
              <div className="flex flex-col gap-2 py-4">
                <Skeleton className="h-12 w-3/4" />
                <Skeleton className="h-12 w-2/3" />
              </div>
            ) : null}
            {error ? (
              <div className="py-3 text-[13px] text-destructive">{error}</div>
            ) : null}
            <ChatThread
              messages={messages}
              streaming={streaming}
              {...(latestStep !== undefined ? { latestStep } : {})}
            />
          </div>
          <ChatInput onSend={send} streaming={streaming} onStop={stop} />
        </section>
      </div>
    </div>
  );
}
