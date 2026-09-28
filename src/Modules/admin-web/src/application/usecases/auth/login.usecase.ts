import type { AuthRepository } from "@/application/abstractions/auth-repository.port";
import type { TokenStorage } from "@/application/abstractions/token-storage.port";
import type { AdminSession } from "@/domain/entities/admin-session.entity";
import { ValidationError } from "@/domain/errors/app-error";
import { validateEmail } from "@/shared/utils/validation";

export interface LoginUseCase {
  execute(email: string, password: string): Promise<AdminSession>;
}

/**
 * Use case: admin sign in.
 * Runs minimal input checks, calls the repository, then stores the token.
 */
export class Login implements LoginUseCase {
  constructor(
    private readonly auth: AuthRepository,
    private readonly tokenStorage: TokenStorage,
  ) {}

  async execute(email: string, password: string): Promise<AdminSession> {
    const emailError = validateEmail(email);
    if (emailError) throw new ValidationError(emailError);
    if (password.length === 0) {
      throw new ValidationError("Password is required.");
    }

    const session = await this.auth.login(email.trim(), password);
    this.tokenStorage.save(session.token);
    return session;
  }
}
