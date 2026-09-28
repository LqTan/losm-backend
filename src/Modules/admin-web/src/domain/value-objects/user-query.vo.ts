import type {
  UserGender,
  UserRole,
  UserStatus,
} from "@/domain/enums/user.enum";
import type { UserScopeFilter } from "@/domain/value-objects/user-scope.vo";
import type { DateRange } from "@/domain/value-objects/date-range.vo";
import type { Pagination } from "@/domain/value-objects/pagination.vo";

/** User list filters, shared by the filter form and the query string. */
export interface UserFilter {
  readonly keyword: string;
  readonly role: UserRole | null;
  readonly status: UserStatus | null;
  readonly gender: UserGender | null;
  readonly createdFrom: string;
  readonly createdTo: string;
}

export const SORT_FIELDS = [
  "createdAt",
  "changedAt",
  "lastLoginAt",
  "username",
  "email",
  "fullName",
  "role",
  "status",
] as const;

export type UserSortField = (typeof SORT_FIELDS)[number];

export interface UserListQuery extends UserFilter {
  /**
   * Slice of accounts this query is limited to. Optional: when omitted the
   * query spans every role.
   */
  readonly scope?: UserScopeFilter;
  readonly sortBy: UserSortField;
  readonly sortDescending: boolean;
  readonly pagination: Pagination;
}

export type UserDateFilter = Partial<Pick<UserFilter, "createdFrom" | "createdTo">>;

export type UserStatsFilter = UserDateFilter & {
  readonly range: DateRange;
};
