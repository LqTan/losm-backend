import { cookies } from "next/headers";
import { AssistantView } from "@/views/assistant-view";
import { AUTH_COOKIE } from "@/shared/config/constants";
import { getInitialUser } from "@/shared/mock/server";
import { redirect } from "next/navigation";

export default async function AssistantPage({
  searchParams,
}: {
  searchParams: Promise<{ session?: string }>;
}) {
  const jar = await cookies();
  const token = jar.get(AUTH_COOKIE)?.value;
  if (!token) redirect("/login");
  const params = await searchParams;

  const user = await getInitialUser();
  const displayName = user?.displayName;

  return (
    <AssistantView
      {...(params.session ? { initialSession: params.session } : {})}
      {...(displayName ? { displayName } : {})}
    />
  );
}
