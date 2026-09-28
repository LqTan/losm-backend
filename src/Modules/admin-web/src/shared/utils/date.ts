import { format, formatDistanceToNowStrict, isValid, parseISO } from "date-fns";
import {
  DATE_FORMAT,
  DATE_TIME_FORMAT,
  ISO_DATE_FORMAT,
} from "@/shared/constants/date-format.constant";

export function toDate(value: string | null | undefined): Date | null {
  if (!value) return null;
  const date = parseISO(value);
  return isValid(date) ? date : null;
}

export function formatDateTime(value: string | null | undefined): string {
  const date = toDate(value);
  return date ? format(date, DATE_TIME_FORMAT) : "—";
}

export function formatDate(value: string | null | undefined): string {
  const date = toDate(value);
  return date ? format(date, DATE_FORMAT) : "—";
}

export function formatRelative(value: string | null | undefined): string {
  const date = toDate(value);
  return date ? `${formatDistanceToNowStrict(date)} ago` : "Never";
}

/** Today as yyyy-MM-dd, for input[type=date] and query parameters. */
export function toIsoDate(value: Date): string {
  return format(value, ISO_DATE_FORMAT);
}

/** Parses "yyyy-MM-dd" into a local Date (avoids the UTC shift of new Date(string)). */
export function fromIsoDate(value: string): Date {
  return new Date(`${value}T00:00:00`);
}

/** Leading initials used as the avatar fallback. */
export function initials(fullName: string): string {
  const parts = fullName.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "?";
  if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase();
  return `${parts[0]![0]}${parts[parts.length - 1]![0]}`.toUpperCase();
}

/** Shortens a long string, for charts and tooltips. */
export function truncate(value: string, maxLength: number): string {
  return value.length <= maxLength ? value : `${value.slice(0, maxLength)}…`;
}
