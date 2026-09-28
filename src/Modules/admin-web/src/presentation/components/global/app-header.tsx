"use client";

import { usePathname, useRouter } from "next/navigation";
import { Dropdown, InlineLoader } from "@/presentation/components/ui";
import { useAuth } from "@/presentation/hooks";
import { initials } from "@/shared/utils/date";

/**
 * Top header. Mazer reserves a small row for the burger button,
 * so the user menu lives here, right aligned.
 */
export function AppHeader() {
  const router = useRouter();
  const pathname = usePathname();
  const { user, logout, isLoading } = useAuth();

  if (isLoading) {
    return (
      <div className="d-flex justify-content-end mb-3">
        <InlineLoader />
      </div>
    );
  }

  return (
    <div className="d-flex align-items-center justify-content-end mb-3">
      {user ? (
        <Dropdown
          align="end"
          trigger={
            <span className="d-flex align-items-center gap-2">
              <span className="user-circle" aria-hidden="true">
                {initials(user.fullName || user.username)}
              </span>
              <span className="d-none d-sm-inline">
                {user.fullName || user.username}
              </span>
            </span>
          }
          items={[
            {
              key: "logout",
              label: "Sign out",
              icon: "bi-box-arrow-right",
              onSelect: () => {
                void logout();
              },
            },
          ]}
        />
      ) : (
        <button
          type="button"
          className="btn btn-sm btn-primary"
          onClick={() => router.push("/login")}
        >
          Sign in
        </button>
      )}
      <span className="visually-hidden">{pathname}</span>
    </div>
  );
}
