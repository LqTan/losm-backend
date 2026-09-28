import { Suspense } from "react";
import type { Metadata } from "next";
import { CreateUserView } from "@/presentation/components/features/users";

export const metadata: Metadata = { title: "New user" };

/**
 * The originating list travels in the query string (?scope=staff|customer) so
 * the form can pin the role and send the admin back to the right list.
 * `useSearchParams` therefore needs a Suspense boundary.
 */
export default function NewUserPage() {
  return (
    <Suspense fallback={null}>
      <CreateUserView />
    </Suspense>
  );
}
