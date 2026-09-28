import type { Metadata } from "next";
import { UsersListView } from "@/presentation/components/features/users";

export const metadata: Metadata = { title: "Customers" };

/** "Customers" — accounts with role = Customer only. */
export default function AdminUsersCustomersPage() {
  return <UsersListView scope="customer" />;
}
