"use client";

import { cn } from "@/shared/utils/classnames";

export interface InputProps
  extends React.InputHTMLAttributes<HTMLInputElement> {
  invalid?: boolean;
  /** Bootstrap Icons class rendered on the left (bi bi-*). */
  iconLeft?: string;
  fieldSize?: "sm" | "md" | "lg";
}

const SIZE_CLASS = {
  sm: "form-control-sm",
  md: "",
  lg: "form-control-lg",
} as const;

/**
 * Bootstrap 5 text input.
 *
 * Note: Mazer's `has-icon-left` style is written as
 * `.form-group[class*="has-icon-"] .form-control-icon { position: absolute }`.
 * so the `form-group` class MUST sit on the same element as
 * `has-icon-left`; otherwise the icon falls below the input instead of sitting inside.
 */
export function Input({
  invalid = false,
  iconLeft,
  fieldSize = "md",
  className,
  ...rest
}: InputProps) {
  const control = cn(
    "form-control",
    SIZE_CLASS[fieldSize],
    invalid && "is-invalid",
    className,
  );

  if (!iconLeft) {
    return <input className={control} {...rest} />;
  }

  return (
    <div className="form-group position-relative has-icon-left mb-0">
      <input className={control} {...rest} />
      <div className="form-control-icon">
        <i className={cn("bi", iconLeft)} aria-hidden="true" />
      </div>
    </div>
  );
}
