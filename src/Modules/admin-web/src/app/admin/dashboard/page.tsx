import type { Metadata } from "next";
import { DashboardView } from "@/presentation/components/features/dashboard";

export const metadata: Metadata = { title: "Dashboard" };

export default function AdminDashboardPage() {
  return <DashboardView />;
}
