import { cn } from "@/shared/utils/classnames";

export type StatsIconTone =
  | "purple"
  | "blue"
  | "green"
  | "red"
  | "orange";

export interface StatCardProps {
  label: string;
  value: number | string;
  hint?: string;
  icon: string;
  tone?: StatsIconTone;
  className?: string;
}

/**
 * Stat card built on Mazer's `.stats-icon` class.
 *
 * Unlike the template's sample markup (which relies on `float`), this uses
 * flex so the icon and text stay vertically aligned at every width —
 * necessary because label lengths vary and `float` makes icons drift.
 */
export function StatCard({
  label,
  value,
  hint,
  icon,
  tone = "blue",
  className,
}: StatCardProps) {
  return (
    <div className={cn("card h-100", className)}>
      <div className="card-body d-flex align-items-center gap-3 px-3 py-3">
        <div className={cn("stats-icon", tone)}>
          <i className={cn("bi", icon)} aria-hidden="true" />
        </div>
        <div className="flex-grow-1 min-w-0">
          <div
            className="text-muted small fw-semibold text-truncate"
            title={label}
          >
            {label}
          </div>
          <div className="fs-4 fw-extrabold lh-sm">{value}</div>
          {hint !== undefined ? (
            <div className="text-muted small text-truncate" title={hint}>
              {hint}
            </div>
          ) : null}
        </div>
      </div>
    </div>
  );
}
