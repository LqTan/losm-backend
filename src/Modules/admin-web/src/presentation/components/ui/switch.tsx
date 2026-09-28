"use client";

import { cn } from "@/shared/utils/classnames";

/** Switch Bootstrap (form-check form-switch), controlled. */
export function Switch({
  id,
  checked,
  onCheckedChange,
  label,
  description,
  disabled = false,
}: {
  id: string;
  checked: boolean;
  onCheckedChange: (checked: boolean) => void;
  label: string;
  description?: string;
  disabled?: boolean;
}) {
  return (
    <div className="form-check form-switch d-flex align-items-start gap-2">
      <input
        className="form-check-input flex-shrink-0"
        type="checkbox"
        role="switch"
        id={id}
        checked={checked}
        disabled={disabled}
        onChange={(event) => onCheckedChange(event.target.checked)}
      />
      <label className="form-check-label" htmlFor={id}>
        <span className={cn("d-block")}>{label}</span>
        {description !== undefined ? (
          <span className="d-block form-text mt-0">{description}</span>
        ) : null}
      </label>
    </div>
  );
}
