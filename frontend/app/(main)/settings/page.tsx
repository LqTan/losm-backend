import { redirect } from "next/navigation";
import { SettingsView } from "@/views/settings-view";
import { getInitialUser } from "@/shared/mock/server";

export default async function SettingsPage() {
  const user = await getInitialUser();
  if (!user) redirect("/login");
  return <SettingsView initialUser={user} />;
}
