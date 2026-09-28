/**
 * The password rules must stay in sync with the backend
 * (Users/Application/Users/Validation/UserInputRules.cs).
 * Validating on the FE only improves UX; the backend remains the source of truth.
 */
export const PASSWORD_RULE = {
  MIN_LENGTH: 8,
  MAX_LENGTH: 128,
  REQUIRES_UPPERCASE: true,
  REQUIRES_DIGIT: true,
} as const;

export const PASSWORD_HINT =
  "At least 8 characters, including 1 uppercase letter and 1 number.";

export function validatePassword(password: string): string | null {
  if (password.length === 0) {
    return "Password is required.";
  }
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
  if (email.trim().length === 0) {
    return "Email is required.";
  }
  if (!/^[^@\s]+@[^@\s]+\.[^@\s]{2,}$/.test(email.trim())) {
    return "Email is not a valid email address.";
  }
  return null;
}

export function validatePhone(phone: string): string | null {
  if (phone.length === 0) return null;
  if (!/^\d+$/.test(phone)) {
    return "Phone number must contain digits only.";
  }
  if (phone.length < 9 || phone.length > 15) {
    return "Phone number must be between 9 and 15 digits.";
  }
  return null;
}
