import type { AuthRepository } from "@/application/abstractions/auth-repository.port";
import type { TokenStorage } from "@/application/abstractions/token-storage.port";

export interface LogoutUseCase {
  execute(): Promise<void>;
}

/**
 * Use case: sign out.
 * Calls the API first (best-effort), then always clears the local token
 * so signing out succeeds even when the network fails.
 */
export class Logout implements LogoutUseCase {
  constructor(
    private readonly auth: AuthRepository,
    private readonly tokenStorage: TokenStorage,
  ) {}

  async execute(): Promise<void> {
    try {
      if (this.tokenStorage.get()) {
        await this.auth.logout();
      }
    } finally {
      this.tokenStorage.clear();
    }
  }
}
