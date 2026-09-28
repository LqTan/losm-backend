import type {
  AuthRepository,
  HttpClient,
} from "@/application/abstractions";
import type { AdminSession } from "@/domain/entities/admin-session.entity";
import type { User } from "@/domain/entities/user.entity";
import type { UserRole } from "@/domain/enums/user.enum";

/**
 * JSON shape returned by the API (the backend converts the PascalCase enums
 * to strings, so they line up with the domain enums).
 */
interface LoginResponseDto {
  readonly id: string;
  readonly username: string;
  readonly email: string;
  readonly fullName: string;
  readonly role: UserRole;
  readonly token: string;
  readonly lastLoginAt: string | null;
  readonly expiresAt: string;
}

export class RestAuthRepository implements AuthRepository {
  constructor(private readonly http: HttpClient) {}

  async login(email: string, password: string): Promise<AdminSession> {
    const { body } = await this.http.send<LoginResponseDto>({
      method: "POST",
      path: "/api/admin/auth/login",
      anonymous: true,
      body: { email, password },
    });

    return {
      user: {
        id: body.id,
        username: body.username,
        email: body.email,
        fullName: body.fullName,
        phone: null,
        gender: null,
        role: body.role,
        status: "Active",
        notifyOnAccountCreation: false,
        lastLoginAt: body.lastLoginAt,
        createdAt: body.expiresAt,
        changedAt: body.expiresAt,
      },
      token: body.token,
      expiresAt: body.expiresAt,
    };
  }

  async logout(): Promise<void> {
    await this.http.send<{ success: boolean }>({
      method: "POST",
      path: "/api/admin/auth/logout",
    });
  }

  async getCurrentProfile(): Promise<User> {
    const { body } = await this.http.send<User>({
      method: "GET",
      path: "/api/admin/auth/me",
    });
    return body;
  }

  async verifyCurrentPassword(password: string): Promise<boolean> {
    const { body } = await this.http.send<{ isValid: boolean }>({
      method: "POST",
      path: "/api/admin/auth/verify-password",
      body: { password },
    });
    return body.isValid;
  }
}
