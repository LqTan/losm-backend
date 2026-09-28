import type { Metadata } from "next";
import { EditUserView } from "@/presentation/components/features/users";

export const metadata: Metadata = { title: "Edit user" };

export default async function EditUserPage({
  params,
}: PageProps<"/admin/users/[id]">) {
  const { id } = await params;
  return <EditUserView userId={id} />;
}
