import type { User } from "@/domain/entities/user.entity";
import type { UserUpdate } from "@/domain/entities/user.entity";
import type { UserListQuery } from "@/domain/value-objects/user-query.vo";
import type { Paginated } from "@/application/abstractions/http-client.port";

/** Payload sent when creating a user (includes the password). */
export interface CreateUserInput extends UserUpdate {
  readonly password: string;
}

export interface UserListInput {
  readonly query: UserListQuery;
}

export type UserListOutput = Paginated<User>;

export interface DeleteUserInput {
  readonly id: string;
}

export interface ResetPasswordInput {
  readonly id: string;
  readonly password: string;
  readonly notify: boolean;
}
