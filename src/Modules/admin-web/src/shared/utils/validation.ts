/**
 * Client-side mirror of the backend validation
 * (Users/Application/Users/Validation/UserInputRules.cs).
 *
 * Validating here only improves UX and avoids a pointless round-trip; the
 * backend stays the source of truth. Keep both files in sync.
 */

export const PASSWORD_RULE = {
  MIN_LENGTH: 8,
  MAX_LENGTH: 128,
  REQUIRES_UPPERCASE: true,
  REQUIRES_DIGIT: true,
} as const;

export const PASSWORD_HINT =
  "At least 8 characters, including 1 uppercase letter and 1 number.";

export const EMAIL_MAX_LENGTH = 255;
export const FULL_NAME_MAX_LENGTH = 255;
export const USERNAME_MAX_LENGTH = 100;

/**
 * Vietnamese phone number: the country/local prefix (84 or 0) followed by
 * 9 digits, e.g. 84397444937 or 0397444937.
 */
const PHONE_PATTERN = /^(84|0)\d{9}$/;
export const PHONE_HINT =
  "Vietnamese number: 84 or 0 followed by 9 digits (e.g. 84397444937).";

/**
 * Personal name: letters of any script (so Vietnamese diacritics and
 * minority names are accepted) plus the apostrophe used in names such as
 * "A-Ma". Digits and other punctuation are rejected.
 */
const FULL_NAME_PATTERN = /^[\p{L}']+(?: [\p{L}']+)*$/u;

export function validatePassword(password: string): string | null {
  if (password.length === 0) return "Password is required.";
  if (password.length < PASSWORD_RULE.MIN_LENGTH) {
    return `Password must be at least ${PASSWORD_RULE.MIN_LENGTH} characters long.`;
  }
  if (password.length > PASSWORD_RULE.MAX_LENGTH) {
    return `Password must be at most ${PASSWORD_RULE.MAX_LENGTH} characters long.`;
  }
  if (PASSWORD_RULE.REQUIRES_UPPERCASE && !/[A-Z]/.test(password)) {
    return "Password must contain at least 1 uppercase letter.";
  }
  if (PASSWORD_RULE.REQUIRES_DIGIT && !/\d/.test(password)) {
    return "Password must contain at least 1 number.";
  }
  return null;
}

export function validateEmail(email: string): string | null {
  if (email.trim().length === 0) return "Email is required.";
  const trimmed = email.trim();
  if (trimmed.length > EMAIL_MAX_LENGTH) {
    return `Email must be at most ${EMAIL_MAX_LENGTH} characters.`;
  }
  if (!/^[^@\s]+@[^@\s]+\.[^@\s]{2,}$/.test(trimmed)) {
    return "Email is not a valid email address.";
  }
  return null;
}

export function validatePhone(phone: string): string | null {
  if (phone.length === 0) return null;
  if (!PHONE_PATTERN.test(phone)) {
    return "Phone must be a Vietnamese number: 84 or 0 followed by 9 digits (e.g. 84397444937 or 0397444937).";
  }
  return null;
}

export function validateFullName(fullName: string): string | null {
  const trimmed = fullName.trim();
  if (trimmed.length === 0) return "Full name is required.";
  if (trimmed.length > FULL_NAME_MAX_LENGTH) {
    return `Full name must be at most ${FULL_NAME_MAX_LENGTH} characters.`;
  }
  if (!FULL_NAME_PATTERN.test(trimmed)) {
    return "Full name must not contain digits or special characters. Only letters and the apostrophe are allowed.";
  }
  return null;
}

export function validateUsername(username: string): string | null {
  const trimmed = username.trim();
  if (trimmed.length === 0) return "Username is required.";
  if (trimmed.length > USERNAME_MAX_LENGTH) {
    return `Username must be at most ${USERNAME_MAX_LENGTH} characters.`;
  }
  return null;
}
