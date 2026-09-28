import type { AuthRepository } from "@/application/abstractions/auth-repository.port";
import type { TokenStorage } from "@/application/abstractions/token-storage.port";
import type { User } from "@/domain/entities/user.entity";

export interface GetCurrentProfileUseCase {
  execute(): Promise<User>;
}

/**
 * Use case: fetch the current admin profile (used to restore the session on F5).
 * If the token is no longer valid, the local token is cleared as well.
 */
export class GetCurrentProfile implements GetCurrentProfileUseCase {
  constructor(
    private readonly auth: AuthRepository,
    private readonly tokenStorage: TokenStorage,
  ) {}

  async execute(): Promise<User> {
    if (!this.tokenStorage.get()) {
      throw new Error("No stored session.");
    }

    try {
      return await this.auth.getCurrentProfile();
    } catch (error) {
      this.tokenStorage.clear();
      throw error;
    }
  }
}
