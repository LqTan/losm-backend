import type { AdminSession } from "@/domain/entities/admin-session.entity";
import type { User } from "@/domain/entities/user.entity";

/**
 * Port: admin session business operations.
 * Use cases go through this port; infrastructure decides which REST endpoint to hit.
 */
export interface AuthRepository {
  login(email: string, password: string): Promise<AdminSession>;
  logout(): Promise<void>;
  getCurrentProfile(): Promise<User>;
  /** Re-checks the password of the currently signed-in admin. */
  verifyCurrentPassword(password: string): Promise<boolean>;
}
