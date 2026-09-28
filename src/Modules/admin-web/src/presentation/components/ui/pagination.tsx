"use client";

import { cn } from "@/shared/utils/classnames";

export interface PaginationProps {
  page: number;
  totalPages: number;
  totalCount: number;
  pageSize: number;
  pageSizeOptions: readonly number[];
  onPageChange: (page: number) => void;
  onPageSizeChange: (pageSize: number) => void;
}

/** Bootstrap pagination: rows-per-page selector plus prev/next. */
export function Pagination({
  page,
  totalPages,
  totalCount,
  pageSize,
  pageSizeOptions,
  onPageChange,
  onPageSizeChange,
}: PaginationProps) {
  const firstRow = totalCount === 0 ? 0 : (page - 1) * pageSize + 1;
  const lastRow = Math.min(page * pageSize, totalCount);

  return (
    <div className="d-flex flex-wrap align-items-center justify-content-between gap-3 pt-3">
      <div className="d-flex align-items-center gap-2">
        <label className="text-muted small mb-0" htmlFor="page-size">
          Rows per page
        </label>
        <select
          id="page-size"
          className="form-select form-select-sm w-auto"
          value={pageSize}
          onChange={(event) => onPageSizeChange(Number(event.target.value))}
        >
          {pageSizeOptions.map((option) => (
            <option key={option} value={option}>
              {option}
            </option>
          ))}
        </select>
        <span className="text-muted small">
          {firstRow}–{lastRow} of {totalCount}
        </span>
      </div>

      <nav aria-label="User list pagination">
        <ul className="pagination pagination-sm mb-0">
          <li className={cn("page-item", page <= 1 && "disabled")}>
            <button
              type="button"
              className="page-link"
              onClick={() => onPageChange(page - 1)}
              disabled={page <= 1}
            >
              Previous
            </button>
          </li>
          <li className="page-item disabled">
            <span className="page-link">
              Page {totalPages === 0 ? 0 : page} of {totalPages}
            </span>
          </li>
          <li className={cn("page-item", page >= totalPages && "disabled")}>
            <button
              type="button"
              className="page-link"
              onClick={() => onPageChange(page + 1)}
              disabled={page >= totalPages}
            >
              Next
            </button>
          </li>
        </ul>
      </nav>
    </div>
  );
}
