/**
 * Enums shared with the backend (Users/Domain/Enums/*.cs).
 * The backend serialises them as strings, so the FE receives the exact names below.
 */

export const USER_ROLES = ["Administrator", "Customer"] as const;
export type UserRole = (typeof USER_ROLES)[number];

export const USER_GENDERS = ["Male", "Female", "Others"] as const;
export type UserGender = (typeof USER_GENDERS)[number];

export const USER_STATUSES = ["Active", "Blocked"] as const;
export type UserStatus = (typeof USER_STATUSES)[number];
