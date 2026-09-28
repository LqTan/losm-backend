"use client";

import { cn } from "@/shared/utils/classnames";

export function Spinner({
  size = "normal",
  className,
  label,
}: {
  size?: "sm" | "normal";
  className?: string;
  label?: string;
}) {
  return (
    <div
      className={cn(
        "d-flex align-items-center gap-2 justify-content-center py-4",
        className,
      )}
      role="status"
    >
      <span
        className={cn(
          "spinner-border",
          size === "sm" && "spinner-border-sm",
        )}
        aria-hidden="true"
      />
      {label !== undefined ? (
        <span className="text-muted small">{label}</span>
      ) : (
        <span className="visually-hidden">Loading…</span>
      )}
    </div>
  );
}

export function InlineLoader({ className }: { className?: string }) {
  return (
    <span
      className={cn("spinner-border spinner-border-sm", className)}
      role="status"
      aria-hidden="true"
    />
  );
}
