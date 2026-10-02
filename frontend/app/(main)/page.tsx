import { redirect } from "next/navigation";
import { MainView } from "@/views/main-view";
import { getInitialUser } from "@/shared/mock/server";

export default async function HomePage() {
  const user = await getInitialUser();
  if (!user) redirect("/login");
  return <MainView initialUser={user} />;
}
