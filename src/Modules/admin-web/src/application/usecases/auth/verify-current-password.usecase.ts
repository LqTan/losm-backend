import type { AuthRepository } from "@/application/abstractions/auth-repository.port";
import { ValidationError } from "@/domain/errors/app-error";

export interface VerifyCurrentPasswordUseCase {
  execute(password: string): Promise<boolean>;
}

/**
 * Use case: confirm the signed-in admin's password before performing a
 * sensitive operation. Returns a bool so the form can show the error inline.
 */
export class VerifyCurrentPassword implements VerifyCurrentPasswordUseCase {
  constructor(private readonly auth: AuthRepository) {}

  async execute(password: string): Promise<boolean> {
    if (password.length === 0) {
      throw new ValidationError(
        "Enter your current password to continue.",
      );
    }
    return this.auth.verifyCurrentPassword(password);
  }
}
