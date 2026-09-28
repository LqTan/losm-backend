import { clsx, type ClassValue } from "clsx";

/**
 * Conditional className builder. Used in every component instead of template
 * literals, to stay type-safe and readable.
 */
export function cn(...inputs: ClassValue[]): string {
  return clsx(inputs);
}
