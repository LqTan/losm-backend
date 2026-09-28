"use client";

import { useSearchParams } from "next/navigation";
import { parseUserScope } from "@/domain/value-objects/user-scope.vo";
import { EditUserView } from "@/presentation/components/features/users/edit-user-view";

/**
 * Client wrapper for the edit route.
 *
 * The originating list travels in the query string (?scope=staff|customer) so
 * saving returns to the list the admin came from. Reading it here (rather than
 * in the page) keeps `useSearchParams` behind the Suspense boundary declared
 * by the route.
 */
export function EditUserPageView({ userId }: { userId: string }) {
  const searchParams = useSearchParams();
  const scope = parseUserScope(searchParams.get("scope"));

  return <EditUserView userId={userId} scope={scope} />;
}
