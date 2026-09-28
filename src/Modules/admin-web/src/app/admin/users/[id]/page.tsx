import { Suspense } from "react";
import type { Metadata } from "next";
import { EditUserPageView } from "@/presentation/components/features/users/edit-user-page";

export const metadata: Metadata = { title: "Edit user" };

export default async function EditUserPage({
  params,
}: PageProps<"/admin/users/[id]">) {
  const { id } = await params;
  return (
    <Suspense fallback={null}>
      <EditUserPageView userId={id} />
    </Suspense>
  );
}
