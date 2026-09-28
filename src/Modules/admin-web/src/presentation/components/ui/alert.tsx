"use client";

import { cn } from "@/shared/utils/classnames";

export type AlertTone = "primary" | "success" | "danger" | "warning" | "info";

export function Alert({
  tone = "danger",
  title,
  children,
  className,
}: {
  tone?: AlertTone;
  title?: string;
  children?: React.ReactNode;
  className?: string;
}) {
  return (
    <div className={cn("alert", `alert-${tone}`, className)} role="alert">
      {title !== undefined ? (
        <h4 className="alert-heading">{title}</h4>
      ) : null}
      {children}
    </div>
  );
}

export function EmptyState({
  icon = "bi-inbox",
  title,
  description,
  action,
}: {
  icon?: string;
  title: string;
  description?: string;
  action?: React.ReactNode;
}) {
  return (
    <div className="text-center py-5">
      <i className={cn("bi", icon, "fs-1 text-muted")} aria-hidden="true" />
      <p className="fw-semibold mb-1 mt-3">{title}</p>
      {description !== undefined ? (
        <p className="text-muted small mb-3">{description}</p>
      ) : null}
      {action}
    </div>
  );
}
