import { USER_ROLES, type UserRole } from "@/domain/enums/user.enum";

/**
 * A user list is always scoped to a slice of the account space so the
 * admin UI never mixes roles in one table.
 *
 * - `staff`    → "User management": every account that is NOT a customer.
 *                Expressed as an exclusion so the view keeps meaning "all
 *                non-customer accounts" if more roles are added later.
 * - `customer` → "Customers": role = Customer.
 */
export const USER_LIST_SCOPES = ["staff", "customer"] as const;
export type UserListScope = (typeof USER_LIST_SCOPES)[number];

export interface UserScopeFilter {
  /** Exact role to match, when the scope is defined by inclusion. */
  readonly role?: UserRole;
  /** Role to leave out, when the scope is defined by exclusion. */
  readonly excludeRole?: UserRole;
}

export const USER_SCOPE_FILTERS: Record<
  UserListScope,
  UserScopeFilter
> = {
  staff: { excludeRole: "Customer" },
  customer: { role: "Customer" },
};

export function isUserListScope(value: string): value is UserListScope {
  return (USER_LIST_SCOPES as readonly string[]).includes(value);
}

export function userScopeFilter(
  scope: UserListScope,
): UserScopeFilter {
  return USER_SCOPE_FILTERS[scope];
}

/**
 * Roles the role dropdown may offer for a given scope.
 *
 * Derived from the scope instead of hard-coded so the options can never
 * contradict the list: the staff scope excludes Customer by definition, so
 * offering it there would let the user pick a role that yields zero rows.
 * An inclusion scope (customer) offers only its single role.
 */
export function userScopeRoles(
  scope: UserListScope,
): readonly UserRole[] {
  const filter = USER_SCOPE_FILTERS[scope];

  if (filter.role !== undefined) {
    return USER_ROLES.filter((role) => role === filter.role);
  }

  return USER_ROLES.filter((role) => role !== filter.excludeRole);
}
