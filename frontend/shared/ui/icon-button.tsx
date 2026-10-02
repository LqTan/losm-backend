"use client";

import type { ButtonHTMLAttributes, ReactNode } from "react";
import { Button } from "@/components/ui/button";
import { cn } from "@/shared/lib/cn";

export interface IconButtonProps
  extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, "children"> {
  readonly icon: ReactNode;
  readonly label: string;
}

export function IconButton({
  icon,
  label,
  className,
  ...rest
}: IconButtonProps) {
  return (
    <Button
      type="button"
      variant="ghost"
      size="icon-sm"
      aria-label={label}
      title={label}
      className={cn(className)}
      {...rest}
    >
      {icon}
      <span className="sr-only">{label}</span>
    </Button>
  );
}
