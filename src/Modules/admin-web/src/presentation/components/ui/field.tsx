"use client";

import { useId } from "react";
import { cn } from "@/shared/utils/classnames";

const SPACING_CLASS = {
  none: "",
  sm: "mb-2",
  md: "mb-3",
  lg: "mb-4",
} as const;

export interface FieldProps {
  label: string;
  htmlFor: string;
  required?: boolean;
  hint?: string;
  error?: string;
  /**
   * Bottom spacing. Use this prop instead of passing an "mb-*" utility via
   * className: clsx does not drop conflicting classes, so both would end up
   * in the HTML and the winner would depend on stylesheet order.
   */
  spacing?: keyof typeof SPACING_CLASS;
  className?: string;
  children: React.ReactNode;
}

/**
 * Consistent wrapper for label + control + hint/error.
 * Reuse it in every form instead of repeating Bootstrap markup.
 */
export function Field({
  label,
  htmlFor,
  required = false,
  hint,
  error,
  spacing = "md",
  className,
  children,
}: FieldProps) {
  return (
    <div className={cn(SPACING_CLASS[spacing], className)}>
      <label className="form-label" htmlFor={htmlFor}>
        {label}
        {required ? <span className="text-danger ms-1">*</span> : null}
      </label>
      {children}
      {error ? (
        <div className="invalid-feedback d-block">{error}</div>
      ) : hint ? (
        <div className="form-text">{hint}</div>
      ) : null}
    </div>
  );
}

/** Generates a stable id for fields without an explicit htmlFor. */
export function useFieldId(prefix: string): string {
  return `${prefix}-${useId()}`;
}
