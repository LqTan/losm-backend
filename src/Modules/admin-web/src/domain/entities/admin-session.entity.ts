import type { User } from "@/domain/entities/user.entity";

/** Admin session; only lives in memory / the token store. */
export interface AdminSession {
  readonly user: User;
  readonly token: string;
  readonly expiresAt: string;
}
