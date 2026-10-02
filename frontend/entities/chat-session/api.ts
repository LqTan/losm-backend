import type {
  ChatSession,
  SessionHistory,
} from "./model";

async function getJson<T>(url: string): Promise<T> {
  const res = await fetch(url, { cache: "no-store" });
  if (!res.ok) throw new Error(`API ${res.status}`);
  return (await res.json()) as T;
}

export async function fetchSessionList(): Promise<readonly ChatSession[]> {
  const data = await getJson<{ items?: ChatSession[] } | ChatSession[]>(
    `/api/proxy/agent/sessions`,
  );
  return Array.isArray(data) ? data : (data.items ?? []);
}

export async function fetchSessionHistory(
  sessionId: string,
): Promise<SessionHistory | null> {
  try {
    return await getJson<SessionHistory>(
      `/api/proxy/agent/sessions/${sessionId}`,
    );
  } catch {
    return null;
  }
}
