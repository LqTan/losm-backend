"use client";

import { cn } from "@/shared/utils/classnames";

export function Table({
  children,
  className,
  responsive = true,
  hover = true,
  size = "sm",
}: {
  children: React.ReactNode;
  className?: string;
  responsive?: boolean;
  hover?: boolean;
  size?: "sm" | "md";
}) {
  const table = (
    <table
      className={cn(
        "table align-middle mb-0",
        size === "sm" && "table-sm",
        hover && "table-hover",
        className,
      )}
    >
      {children}
    </table>
  );

  return responsive ? (
    <div className="table-responsive">{table}</div>
  ) : (
    table
  );
}

export function Thead({ children }: { children: React.ReactNode }) {
  return <thead>{children}</thead>;
}

export function Tbody({ children }: { children: React.ReactNode }) {
  return <tbody>{children}</tbody>;
}

export function Tr({
  children,
  className,
}: {
  children: React.ReactNode;
  className?: string;
}) {
  return <tr className={className}>{children}</tr>;
}

export function Th({
  children,
  className,
  align = "start",
}: {
  children?: React.ReactNode;
  className?: string;
  align?: "start" | "center" | "end";
}) {
  return (
    <th
      scope="col"
      className={cn(
        align === "center" && "text-center",
        align === "end" && "text-end",
        className,
      )}
    >
      {children}
    </th>
  );
}

export function Td({
  children,
  className,
  align = "start",
  colSpan,
}: {
  children?: React.ReactNode;
  className?: string;
  align?: "start" | "center" | "end";
  /** Span multiple columns, e.g. for a full-width empty/loading row. */
  colSpan?: number;
}) {
  return (
    <td
      colSpan={colSpan}
      className={cn(
        align === "center" && "text-center",
        align === "end" && "text-end",
        className,
      )}
    >
      {children}
    </td>
  );
}
