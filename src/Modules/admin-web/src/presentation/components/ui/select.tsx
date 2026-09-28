"use client";

import { cn } from "@/shared/utils/classnames";

export interface SelectOption<T extends string = string> {
  readonly value: T;
  readonly label: string;
  readonly disabled?: boolean;
}

export interface SelectProps<T extends string = string>
  extends Omit<React.SelectHTMLAttributes<HTMLSelectElement>, "onChange"> {
  options: readonly SelectOption<T>[];
  /** The first option acts as the placeholder; value="" maps to null. */
  placeholder?: string;
  invalid?: boolean;
  fieldSize?: "sm" | "md" | "lg";
  onValueChange?: (value: T | null) => void;
}

const SIZE_CLASS = {
  sm: "form-select-sm",
  md: "",
  lg: "form-select-lg",
} as const;

/**
 * Bootstrap's native select element.
 * Deliberately a plain <select> rather than a custom combobox: every option
 * carries its own label, so the raw value can never leak and no items prop is needed.
 */
export function Select<T extends string = string>({
  options,
  placeholder,
  invalid = false,
  fieldSize = "md",
  onValueChange,
  className,
  ...rest
}: SelectProps<T>) {
  return (
    <select
      className={cn(
        "form-select",
        SIZE_CLASS[fieldSize],
        invalid && "is-invalid",
        className,
      )}
      onChange={(event) => {
        const raw = event.target.value;
        onValueChange?.(raw === "" ? null : (raw as T));
      }}
      {...rest}
    >
      {placeholder !== undefined ? (
        <option value="">{placeholder}</option>
      ) : null}
      {options.map((option) => (
        <option
          key={option.value}
          value={option.value}
          disabled={option.disabled}
        >
          {option.label}
        </option>
      ))}
    </select>
  );
}
