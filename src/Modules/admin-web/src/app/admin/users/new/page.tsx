import type { Metadata } from "next";
import { CreateUserView } from "@/presentation/components/features/users";

export const metadata: Metadata = { title: "New user" };

export default function NewUserPage() {
  return <CreateUserView />;
}
