import type {
  CreateUserInput,
  HttpClient,
  UserListOutput,
  UserRepository,
} from "@/application/abstractions";
import type { User, UserUpdate } from "@/domain/entities/user.entity";
import type { UserListQuery } from "@/domain/value-objects/user-query.vo";

const BASE_PATH = "/api/admin/users";

function toQuery(query: UserListQuery) {
  return {
    keyword: query.keyword || undefined,
    // The scope narrows the account space; an explicit role filter from the
    // form still wins when both are present.
    role: query.scope?.role ?? query.role ?? undefined,
    excludeRole: query.scope?.excludeRole ?? undefined,
    status: query.status ?? undefined,
    gender: query.gender ?? undefined,
    from: query.createdFrom || undefined,
    to: query.createdTo || undefined,
    sortBy: query.sortBy,
    sortDescending: query.sortDescending,
    page: query.pagination.page,
    pageSize: query.pagination.pageSize,
  };
}

export class RestUserRepository implements UserRepository {
  constructor(private readonly http: HttpClient) {}

  async list(query: UserListQuery): Promise<UserListOutput> {
    const { body } = await this.http.send<UserListOutput>({
      method: "GET",
      path: BASE_PATH,
      query: toQuery(query),
    });
    return body;
  }

  async getById(id: string): Promise<User> {
    const { body } = await this.http.send<User>({
      method: "GET",
      path: `${BASE_PATH}/${id}`,
    });
    return body;
  }

  async create(input: CreateUserInput): Promise<User> {
    const { body } = await this.http.send<User>({
      method: "POST",
      path: BASE_PATH,
      body: {
        username: input.username,
        email: input.email,
        fullName: input.fullName,
        phone: input.phone,
        gender: input.gender,
        role: input.role,
        status: input.status,
        password: input.password,
        notify: input.notify,
      },
    });
    return body;
  }

  async update(id: string, input: UserUpdate): Promise<User> {
    const { body } = await this.http.send<User>({
      method: "PUT",
      path: `${BASE_PATH}/${id}`,
      body: {
        username: input.username,
        email: input.email,
        fullName: input.fullName,
        phone: input.phone,
        gender: input.gender,
        role: input.role,
        status: input.status,
        notify: input.notify,
      },
    });
    return body;
  }

  async remove(id: string): Promise<void> {
    await this.http.send<void>({
      method: "DELETE",
      path: `${BASE_PATH}/${id}`,
    });
  }

  async resetPassword(
    id: string,
    password: string,
    notify: boolean,
  ): Promise<void> {
    await this.http.send<void>({
      method: "POST",
      path: `${BASE_PATH}/${id}/password`,
      body: { password, notify },
    });
  }
}
