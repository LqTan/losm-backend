import type { UserId } from "@/shared/lib/brands";

export interface User {
  readonly id: UserId;
  readonly email: string;
  readonly username: string;
  readonly displayName: string;
  readonly createdAt: string;
}

export interface LoginResponse {
  readonly id: UserId;
  readonly username: string;
  readonly email: string;
  readonly token: string;
}

export interface CurrentSessionResponse {
  readonly user: User;
}
