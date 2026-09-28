import type { AdminSession } from "@/domain/entities/admin-session.entity";
import type { User } from "@/domain/entities/user.entity";

/**
 * Input/output types for the auth use cases.
 * Kept separate from the entities because the API JSON shape may differ from the domain shape.
 */
export interface LoginInput {
  readonly email: string;
  readonly password: string;
}

export interface LoginOutput {
  readonly session: AdminSession;
}

export interface CurrentProfileOutput {
  readonly user: User;
}
