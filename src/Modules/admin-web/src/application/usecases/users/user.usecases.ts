import type { UserRepository } from "@/application/abstractions/user-repository.port";
import type { CreateUserInput, UserListOutput } from "@/application/dto/user.dto";
import type { User, UserUpdate } from "@/domain/entities/user.entity";
import type { UserListQuery } from "@/domain/value-objects/user-query.vo";
import { ValidationError } from "@/domain/errors/app-error";
import { normalizePagination } from "@/domain/value-objects/pagination.vo";
import {
  validateEmail,
  validatePassword,
  validatePhone,
} from "@/shared/utils/validation";

export interface ListUsersUseCase {
  execute(query: UserListQuery): Promise<UserListOutput>;
}

export class ListUsers implements ListUsersUseCase {
  constructor(private readonly users: UserRepository) {}

  execute(query: UserListQuery): Promise<UserListOutput> {
    return this.users.list({
      ...query,
      pagination: normalizePagination(query.pagination),
    });
  }
}

export interface GetUserUseCase {
  execute(id: string): Promise<User>;
}

export class GetUser implements GetUserUseCase {
  constructor(private readonly users: UserRepository) {}

  execute(id: string): Promise<User> {
    return this.users.getById(id);
  }
}

export interface CreateUserUseCase {
  execute(input: CreateUserInput): Promise<User>;
}

/** Validation shared by the create and update use cases. */
function assertUserInput(
  input: Omit<UserUpdate, "notify">,
  options: { requirePassword: boolean; password?: string },
) {
  const errors: string[] = [];

  if (input.fullName.trim().length === 0) {
    errors.push("Full name is required.");
  }
  if (input.username.trim().length === 0) {
    errors.push("Username is required.");
  }

  const emailError = validateEmail(input.email);
  if (emailError) errors.push(emailError);

  const phoneError = validatePhone(input.phone ?? "");
  if (phoneError) errors.push(phoneError);

  if (options.requirePassword) {
    const passwordError = validatePassword(options.password ?? "");
    if (passwordError) errors.push(passwordError);
  }

  if (errors.length > 0) {
    throw new ValidationError(errors[0]!);
  }
}

export class CreateUser implements CreateUserUseCase {
  constructor(private readonly users: UserRepository) {}

  async execute(input: CreateUserInput): Promise<User> {
    assertUserInput(input, {
      requirePassword: true,
      password: input.password,
    });
    return this.users.create(input);
  }
}

export interface UpdateUserUseCase {
  execute(id: string, input: UserUpdate): Promise<User>;
}

export class UpdateUser implements UpdateUserUseCase {
  constructor(private readonly users: UserRepository) {}

  async execute(id: string, input: UserUpdate): Promise<User> {
    assertUserInput(input, { requirePassword: false });
    return this.users.update(id, input);
  }
}

export interface DeleteUserUseCase {
  execute(id: string): Promise<void>;
}

export class DeleteUser implements DeleteUserUseCase {
  constructor(private readonly users: UserRepository) {}

  execute(id: string): Promise<void> {
    return this.users.remove(id);
  }
}

export interface ResetUserPasswordUseCase {
  execute(id: string, password: string, notify: boolean): Promise<void>;
}

export class ResetUserPassword implements ResetUserPasswordUseCase {
  constructor(private readonly users: UserRepository) {}

  execute(id: string, password: string, notify: boolean): Promise<void> {
    const passwordError = validatePassword(password);
    if (passwordError) throw new ValidationError(passwordError);
    return this.users.resetPassword(id, password, notify);
  }
}
