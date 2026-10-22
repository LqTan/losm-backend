// API messages are display text, never status identifiers or control-flow values.
export function getApiErrorMessage(data: unknown, fallback: string): string {
  if (typeof data === "string" && data.trim()) return data;
  if (data && typeof data === "object") {
    const body = data as Record<string, unknown>;
    for (const key of ["message", "error", "detail", "title"]) {
      if (typeof body[key] === "string" && body[key].trim()) return body[key];
    }
  }
  return fallback;
}
