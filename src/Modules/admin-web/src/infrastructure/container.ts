import type {
  AuthRepository,
  DashboardRepository,
  HttpClient,
  TokenStorage,
  UnauthorizedNotifier,
  UserRepository,
} from "@/application/abstractions";
import { GetCurrentProfile } from "@/application/usecases/auth/get-current-profile.usecase";
import { Login } from "@/application/usecases/auth/login.usecase";
import { Logout } from "@/application/usecases/auth/logout.usecase";
import { VerifyCurrentPassword } from "@/application/usecases/auth/verify-current-password.usecase";
import { GetDashboardStats } from "@/application/usecases/dashboard/get-dashboard-stats.usecase";
import {
  CreateUser,
  DeleteUser,
  GetUser,
  ListUsers,
  ResetUserPassword,
  UpdateUser,
} from "@/application/usecases/users/user.usecases";
import { FetchHttpClient } from "@/infrastructure/http/fetch-http-client";
import { RestAuthRepository } from "@/infrastructure/repositories/rest-auth.repository";
import { RestDashboardRepository } from "@/infrastructure/repositories/rest-dashboard.repository";
import { RestUserRepository } from "@/infrastructure/repositories/rest-user.repository";
import {
  LocalTokenStorage,
  LocalUnauthorizedNotifier,
} from "@/infrastructure/storage/local-token-storage";

/**
 * Composition root — a stand-in for a DI container.
 *
 * This is where each port is bound to an adapter and each use case to a port.
 * Swapping the HTTP client or the storage only touches this file; no other
 * layer changes.
 */
function createContainer() {
  const tokenStorage = new LocalTokenStorage();
  const unauthorizedNotifier = new LocalUnauthorizedNotifier();
  const http: HttpClient = new FetchHttpClient(
    tokenStorage,
    unauthorizedNotifier,
  );

  const authRepository: AuthRepository = new RestAuthRepository(http);
  const userRepository: UserRepository = new RestUserRepository(http);
  const dashboardRepository: DashboardRepository =
    new RestDashboardRepository(http);

  return {
    tokenStorage: tokenStorage as TokenStorage,
    unauthorizedNotifier: unauthorizedNotifier as UnauthorizedNotifier,

    usecases: {
      auth: {
        login: new Login(authRepository, tokenStorage),
        logout: new Logout(authRepository, tokenStorage),
        getCurrentProfile: new GetCurrentProfile(
          authRepository,
          tokenStorage,
        ),
        verifyCurrentPassword: new VerifyCurrentPassword(
          authRepository,
        ),
      },
      users: {
        list: new ListUsers(userRepository),
        getById: new GetUser(userRepository),
        create: new CreateUser(userRepository),
        update: new UpdateUser(userRepository),
        remove: new DeleteUser(userRepository),
        resetPassword: new ResetUserPassword(userRepository),
      },
      dashboard: {
        getStats: new GetDashboardStats(dashboardRepository),
      },
    },
  };
}

type Container = ReturnType<typeof createContainer>;

let container: Container | null = null;

/** Singleton so adapters are not rebuilt on every render. */
export function getContainer(): Container {
  if (container === null) {
    container = createContainer();
  }
  return container;
}

export type { Container };
