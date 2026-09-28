/**
 * Value object: pagination and sorting parameters.
 * pageSize is clamped here so every layer shares one rule.
 */
export interface Pagination {
  readonly page: number;
  readonly pageSize: number;
}

export const PAGE_SIZE_OPTIONS = [10, 20, 50, 100] as const;
export const DEFAULT_PAGE_SIZE = 20;
export const MAX_PAGE_SIZE = 200;

export function normalizePagination(pagination: Pagination): Pagination {
  return {
    page: pagination.page < 1 ? 1 : pagination.page,
    pageSize: Math.min(Math.max(pagination.pageSize, 1), MAX_PAGE_SIZE),
  };
}

export function totalPages(totalCount: number, pageSize: number): number {
  if (pageSize <= 0) return 0;
  return Math.ceil(totalCount / pageSize);
}
