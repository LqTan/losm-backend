import { redirect } from "next/navigation";

/**
 * The user list is always scoped (staff vs. customer), so the bare
 * /admin/users route just forwards to the default scope.
 */
export default function AdminUsersPage() {
  redirect("/admin/users/administrators");
}
