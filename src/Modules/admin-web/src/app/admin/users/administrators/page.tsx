import type { Metadata } from "next";
import { UsersListView } from "@/presentation/components/features/users";

export const metadata: Metadata = { title: "User management" };

/**
 * "User management" — every account that is NOT a customer.
 * The scope itself lives in domain/value-objects/user-scope.vo.
 */
export default function AdminUsersAdministratorsPage() {
  return <UsersListView scope="staff" />;
}
