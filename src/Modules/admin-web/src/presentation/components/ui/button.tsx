"use client";

import { cn } from "@/shared/utils/classnames";

export type ButtonVariant =
  | "primary"
  | "secondary"
  | "success"
  | "danger"
  | "warning"
  | "info"
  | "light"
  | "dark"
  | "link"
  | "outline-primary"
  | "outline-secondary"
  | "outline-danger";

export type ButtonSize = "sm" | "md" | "lg";

export interface ButtonProps
  extends React.ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: ButtonVariant;
  size?: ButtonSize;
  /** Bootstrap `.block` — stretch to the full container width. */
  block?: boolean;
  loading?: boolean;
  icon?: string;
}

const VARIANT_CLASS: Record<ButtonVariant, string> = {
  primary: "btn-primary",
  secondary: "btn-secondary",
  success: "btn-success",
  danger: "btn-danger",
  warning: "btn-warning",
  info: "btn-info",
  light: "btn-light",
  dark: "btn-dark",
  link: "btn-link",
  "outline-primary": "btn-outline-primary",
  "outline-secondary": "btn-outline-secondary",
  "outline-danger": "btn-outline-danger",
};

export function Button({
  variant = "primary",
  size = "md",
  block = false,
  loading = false,
  icon,
  className,
  children,
  disabled,
  type = "button",
  ...rest
}: ButtonProps) {
  return (
    <button
      type={type}
      className={cn(
        "btn",
        VARIANT_CLASS[variant],
        size === "sm" && "btn-sm",
        size === "lg" && "btn-lg",
        block && "block",
        loading && "disabled",
        className,
      )}
      disabled={disabled || loading}
      {...rest}
    >
      {loading ? (
        <span
          className="spinner-border spinner-border-sm me-2"
          aria-hidden="true"
        />
      ) : icon ? (
        <i className={cn("bi", icon)} aria-hidden="true" />
      ) : null}
      {children}
    </button>
  );
}
