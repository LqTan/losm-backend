import type {
  UserGender,
  UserRole,
  UserStatus,
} from "@/domain/enums/user.enum";

/**
 * A user account. This is the domain model: the backend already
 * normalised every field (role/gender/status are strings, not numbers).
 */
export interface User {
  readonly id: string;
  readonly username: string;
  readonly email: string;
  readonly fullName: string;
  readonly phone: string | null;
  readonly gender: UserGender | null;
  readonly role: UserRole;
  readonly status: UserStatus;
  readonly notifyOnAccountCreation: boolean;
  readonly lastLoginAt: string | null;
  readonly createdAt: string;
  readonly changedAt: string;
}

/**
 * Trimmed projection used by the list table — omits the notify field.
 */
export type UserListItem = Omit<User, "notifyOnAccountCreation">;

/** Only used when creating. */
export interface UserDraft {
  username: string;
  email: string;
  fullName: string;
  phone: string | null;
  gender: UserGender | null;
  role: UserRole;
  status: UserStatus;
}

/** On update the password is optional (empty = keep current). */
export interface UserUpdate extends UserDraft {
  notify: boolean;
}

export function isActive(user: Pick<User, "status">): boolean {
  return user.status === "Active";
}

export function isAdministrator(user: Pick<User, "role">): boolean {
  return user.role === "Administrator";
}

export function fullNameOrUsername(user: Pick<User, "fullName" | "username">) {
  return user.fullName.trim().length > 0 ? user.fullName : user.username;
}
