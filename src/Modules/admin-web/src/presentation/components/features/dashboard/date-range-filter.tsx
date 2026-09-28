"use client";

import { subDays } from "date-fns";
import { DEFAULT_RANGE_DAYS } from "@/domain/value-objects/date-range.vo";
import type { DateRange } from "@/domain/value-objects/date-range.vo";
import { Button, Field, Input, Select } from "@/presentation/components/ui";
import { toIsoDate } from "@/shared/utils/date";

const PRESETS = [
  { label: "Last 7 days", days: 6 },
  { label: "Last 30 days", days: 29 },
  { label: "Last 90 days", days: 89 },
  { label: "Last 12 months", days: 364 },
] as const;

const PRESET_OPTIONS = PRESETS.map((preset) => ({
  value: String(preset.days),
  label: preset.label,
}));

export function defaultDateRange(): DateRange {
  const today = new Date();
  return {
    from: toIsoDate(subDays(today, DEFAULT_RANGE_DAYS - 1)),
    to: toIsoDate(today),
  };
}

export interface DateRangeFilterProps {
  range: DateRange;
  /** Days of the selected preset; null means a custom range. */
  presetDays: number | null;
  onPresetChange: (days: number) => void;
  onRangeChange: (range: DateRange) => void;
}

/**
 * Date range filter: quick presets plus free-form date picking.
 * Uses the native input[type=date] so no calendar library is needed.
 *
 * All three controls sit inside a labelled `Field` so they line up —
 * a bare select would sit lower than the two date inputs.
 */
export function DateRangeFilter({
  range,
  presetDays,
  onPresetChange,
  onRangeChange,
}: DateRangeFilterProps) {
  return (
    <div className="d-flex flex-wrap align-items-end gap-2">
      <Field label="Period" htmlFor="quick-range" spacing="none">
        <Select
          id="quick-range"
          options={PRESET_OPTIONS}
          placeholder="Custom range"
          value={presetDays === null ? "" : String(presetDays)}
          onValueChange={(next) => {
            if (next) onPresetChange(Number(next));
          }}
        />
      </Field>

      <Field label="From" htmlFor="range-from" spacing="none">
        <Input
          id="range-from"
          type="date"
          value={range.from}
          onChange={(event) =>
            onRangeChange({ ...range, from: event.target.value })
          }
        />
      </Field>

      <Field label="To" htmlFor="range-to" spacing="none">
        <Input
          id="range-to"
          type="date"
          value={range.to}
          onChange={(event) =>
            onRangeChange({ ...range, to: event.target.value })
          }
        />
      </Field>
    </div>
  );
}

export { PRESETS };

/** Refresh button shared by the dashboard. */
export function RefreshButton({
  onClick,
  loading,
}: {
  onClick: () => void;
  loading: boolean;
}) {
  return (
    <Button
      variant="light"
      icon="bi-arrow-clockwise"
      aria-label="Refresh"
      onClick={onClick}
      loading={loading}
    />
  );
}
