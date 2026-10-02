import { redirect } from "next/navigation";
import { ProfileView } from "@/views/profile-view";
import { getInitialUser } from "@/shared/mock/server";

export default async function ProfilePage() {
  const user = await getInitialUser();
  if (!user) redirect("/login");
  return <ProfileView initialUser={user} />;
}
