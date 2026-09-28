import type { User, UserUpdate } from "@/domain/entities/user.entity";
import type { UserListQuery } from "@/domain/value-objects/user-query.vo";
import type {
  CreateUserInput,
  UserListOutput,
} from "@/application/dto/user.dto";

export interface UserRepository {
  list(query: UserListQuery): Promise<UserListOutput>;
  getById(id: string): Promise<User>;
  create(input: CreateUserInput): Promise<User>;
  update(id: string, input: UserUpdate): Promise<User>;
  remove(id: string): Promise<void>;
  resetPassword(id: string, password: string, notify: boolean): Promise<void>;
}
