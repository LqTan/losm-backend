"use client";

import { cn } from "@/shared/utils/classnames";

/**
 * CSS grid used in place of Bootstrap's 12-column system.
 *
 * Why: `col-xl-2` is percentage based, leaving each card about 130px wide,
 * so labels wrap and cards end up uneven. With `minColumnWidth` driven by
 * the content itself, columns only increase when there is genuinely room.
 */
export function Grid({
  children,
  /** Fixed column count used when `minColumnWidth` is not set. */
  columns = 2,
  /** Minimum cell width; switches to auto-fit instead of a fixed count. */
  minColumnWidth,
  className,
}: {
  children: React.ReactNode;
  columns?: 1 | 2 | 3 | 4;
  minColumnWidth?: string;
  className?: string;
}) {
  return (
    <div
      className={cn(
        "ls-grid",
        minColumnWidth ? "ls-grid-auto" : `ls-grid-cols-${columns}`,
        className,
      )}
      role="list"
    >
      {children}
    </div>
  );
}

export function GridItem({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return (
    <div className={cn("ls-grid-item", className)} role="listitem">
      {children}
    </div>
  );
}
