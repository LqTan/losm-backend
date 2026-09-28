"use client";

import { cn } from "@/shared/utils/classnames";
import type {
  UserGender,
  UserRole,
  UserStatus,
} from "@/domain/enums/user.enum";

export type BadgeTone =
  | "primary"
  | "secondary"
  | "success"
  | "danger"
  | "warning"
  | "info"
  | "light"
  | "dark";

export function Badge({
  children,
  tone = "secondary",
  className,
}: {
  children: React.ReactNode;
  tone?: BadgeTone;
  className?: string;
}) {
  return (
    <span
      className={cn(
        "badge",
        tone === "light" ? "text-dark bg-light border" : `bg-${tone}`,
        className,
      )}
    >
      {children}
    </span>
  );
}

export function StatusBadge({ status }: { status: UserStatus }) {
  return (
    <Badge tone={status === "Active" ? "success" : "danger"}>{status}</Badge>
  );
}

export function RoleBadge({ role }: { role: UserRole }) {
  return (
    <Badge tone={role === "Administrator" ? "primary" : "secondary"}>
      {role}
    </Badge>
  );
}

export function GenderBadge({ gender }: { gender: UserGender | null }) {
  if (gender === null) {
    return <span className="text-muted">&mdash;</span>;
  }
  return <Badge tone="info">{gender}</Badge>;
}
