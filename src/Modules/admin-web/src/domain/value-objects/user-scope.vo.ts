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

/**
 * Route of each scope. Single source of truth: the sidebar links, the
 * "New user" button and the post-create redirect all resolve through this so
 * they can never drift apart.
 */
export const USER_SCOPE_PATHS: Record<UserListScope, string> = {
  staff: "/admin/users/administrators",
  customer: "/admin/users/customers",
};

export function userScopePath(scope: UserListScope): string {
  return USER_SCOPE_PATHS[scope];
}

/** Parses a scope from a query string, falling back to the default scope. */
export function parseUserScope(
  value: string | null | undefined,
): UserListScope {
  const candidate = value ?? "";
  return isUserListScope(candidate) ? candidate : "staff";
}

export function userScopeFilter(
  scope: UserListScope,
): UserScopeFilter {
  return USER_SCOPE_FILTERS[scope];
}

/**
 * Roles valid inside a scope, used both by the list filter and by the create
 * form's role picker.
 *
 * Derived from the scope so a selector can never offer a role that contradicts
 * the current context: the staff scope excludes Customer by definition, so
 * offering it there would let the admin pick a role whose account then never
 * shows up in the list it was created from.
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

/**
 * True when the scope fixes the role, leaving nothing to choose.
 *
 * Only an inclusion scope (customer) does. The staff scope merely excludes
 * Customer from its list, so its form keeps an editable role picker listing
 * every non-customer role.
 */
export function isRolePinnedByScope(scope: UserListScope): boolean {
  return USER_SCOPE_FILTERS[scope].role !== undefined;
}
