export function isCurrentUserOrigin(origin: { isMe?: boolean; name?: string }): boolean {
  // Explicit API metadata takes precedence; names only support older responses.
  return origin.isMe ?? ["tôi", "me", "you"].includes(origin.name?.trim().toLowerCase() ?? "");
}

export function getOriginInitial(name: string): string {
  const numbered = name.trim().match(/^(?:người|person)\s+(\d+)/i);
  return numbered?.[1] ?? (name.trim().charAt(0).toUpperCase() || "T");
}
