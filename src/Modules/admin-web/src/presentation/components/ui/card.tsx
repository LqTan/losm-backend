"use client";

import { cn } from "@/shared/utils/classnames";

export interface CardProps {
  children: React.ReactNode;
  className?: string;
}

export function Card({ children, className }: CardProps) {
  return <div className={cn("card", className)}>{children}</div>;
}

export interface CardHeaderProps {
  title?: React.ReactNode;
  subtitle?: React.ReactNode;
  /** Action buttons rendered to the right of the title (e.g. New user, Refresh). */
  actions?: React.ReactNode;
  className?: string;
}

export function CardHeader({
  title,
  subtitle,
  actions,
  className,
}: CardHeaderProps) {
  if (title === undefined && actions === undefined) {
    return <div className={cn("card-header", className)} />;
  }

  return (
    <div
      className={cn(
        "card-header d-flex align-items-center justify-content-between gap-3",
        className,
      )}
    >
      <div>
        {title !== undefined ? <h4 className="card-title mb-0">{title}</h4> : null}
        {subtitle !== undefined ? (
          <p className="text-muted mb-0 mt-1 small">{subtitle}</p>
        ) : null}
      </div>
      {actions !== undefined ? (
        <div className="d-flex align-items-center gap-2">{actions}</div>
      ) : null}
    </div>
  );
}

export function CardBody({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return <div className={cn("card-body", className)}>{children}</div>;
}
