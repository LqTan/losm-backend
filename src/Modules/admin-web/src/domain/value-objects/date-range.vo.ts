/**
 * Value object: the date range used to filter the dashboard.
 * Always normalised to local start/end of day to avoid timezone drift.
 */
export interface DateRange {
  readonly from: string; // yyyy-MM-dd
  readonly to: string; // yyyy-MM-dd
}

export const DEFAULT_RANGE_DAYS = 30;

export function daysBetween(range: DateRange): number {
  const from = new Date(`${range.from}T00:00:00`).getTime();
  const to = new Date(`${range.to}T00:00:00`).getTime();
  return Math.round((to - from) / 86_400_000);
}

export function isValidRange(range: DateRange): boolean {
  return (
    /^\d{4}-\d{2}-\d{2}$/.test(range.from) &&
    /^\d{4}-\d{2}-\d{2}$/.test(range.to) &&
    range.from <= range.to
  );
}
