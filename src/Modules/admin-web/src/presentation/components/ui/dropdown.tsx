"use client";

import { useState, type ReactNode } from "react";
import { cn } from "@/shared/utils/classnames";
import { useClickOutside } from "@/presentation/hooks";

export interface DropdownItem {
  readonly key: string;
  readonly label: string;
  readonly icon?: string;
  readonly onSelect: () => void;
  readonly tone?: "default" | "danger";
  readonly disabled?: boolean;
}

/** Bootstrap dropdown; open/close driven by React state instead of data-toggle. */
export function Dropdown({
  trigger,
  items,
  align = "end",
  className,
  triggerClassName,
}: {
  trigger: ReactNode;
  items: readonly DropdownItem[];
  align?: "start" | "end";
  className?: string;
  triggerClassName?: string;
}) {
  const [open, setOpen] = useState(false);
  const ref = useClickOutside<HTMLDivElement>(open, () => setOpen(false));

  return (
    <div className={cn("dropdown", className)} ref={ref}>
      <button
        type="button"
        className={cn("btn user-menu-btn", triggerClassName)}
        aria-expanded={open}
        aria-haspopup="menu"
        onClick={() => setOpen((value) => !value)}
      >
        {trigger}
      </button>

      <div
        className={cn(
          "dropdown-menu",
          align === "end" ? "dropdown-menu-end" : "dropdown-menu-start",
          open && "show",
        )}
        role="menu"
      >
        {items.map((item) => (
          <button
            key={item.key}
            type="button"
            role="menuitem"
            disabled={item.disabled}
            className={cn(
              "dropdown-item",
              item.tone === "danger" &&
                "text-danger d-flex align-items-center",
            )}
            onClick={() => {
              setOpen(false);
              item.onSelect();
            }}
          >
            {item.icon !== undefined ? (
              <i className={cn("bi", item.icon, "me-2")} aria-hidden="true" />
            ) : null}
            {item.label}
          </button>
        ))}
      </div>
    </div>
  );
}

/** Simple icon button used in a table's Actions column. */
export function IconButton({
  icon,
  label,
  onClick,
  tone = "secondary",
  disabled = false,
}: {
  icon: string;
  label: string;
  onClick: () => void;
  tone?: "secondary" | "danger" | "primary";
  disabled?: boolean;
}) {
  return (
    <button
      type="button"
      className={cn("btn btn-sm", `btn-outline-${tone}`)}
      aria-label={label}
      title={label}
      disabled={disabled}
      onClick={onClick}
    >
      <i className={cn("bi", icon)} aria-hidden="true" />
    </button>
  );
}
