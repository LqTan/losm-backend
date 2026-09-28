"use client";

import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useState,
  type ReactNode,
} from "react";
import { useRouter } from "next/navigation";
import { toDisplayMessage } from "@/domain/errors/app-error";
import type { User } from "@/domain/entities/user.entity";
import { getContainer } from "@/infrastructure/container";

interface AuthContextValue {
  readonly user: User | null;
  readonly isLoading: boolean;
  login: (email: string, password: string) => Promise<void>;
  logout: () => Promise<void>;
}

const AuthContext = createContext<AuthContextValue | null>(null);

/**
 * Global auth provider.
 * - Restores the session on F5 via the GetCurrentProfile use case.
 * - Listens for the infrastructure unauthorized event to reset state.
 */
export function AuthProvider({ children }: { children: ReactNode }) {
  const router = useRouter();
  const [user, setUser] = useState<User | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const login = useCallback(async (email: string, password: string) => {
    const container = getContainer();
    await container.usecases.auth.login.execute(email, password);
    const profile = await container.usecases.auth.getCurrentProfile.execute();
    setUser(profile);
  }, []);

  const logout = useCallback(async () => {
    const container = getContainer();
    await container.usecases.auth.logout.execute();
    setUser(null);
    setIsLoading(false);
    router.replace("/login");
  }, [router]);

  useEffect(() => {
    const container = getContainer();

    container.unauthorizedNotifier.onUnauthorized(() => {
      container.tokenStorage.clear();
      setUser(null);
    });

    void (async () => {
      try {
        if (!container.tokenStorage.get()) return;
        const profile =
          await container.usecases.auth.getCurrentProfile.execute();
        setUser(profile);
      } catch (error) {
        if (error instanceof Error && error.message !== NO_SESSION_MESSAGE) {
          console.warn("[auth] failed to restore session:", toDisplayMessage(error));
        }
      } finally {
        setIsLoading(false);
      }
    })();
  }, []);

  const value = useMemo<AuthContextValue>(
    () => ({ user, isLoading, login, logout }),
    [user, isLoading, login, logout],
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

const NO_SESSION_MESSAGE = "No stored session.";

export function useAuth(): AuthContextValue {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error("useAuth must be used inside <AuthProvider>.");
  }
  return context;
}
