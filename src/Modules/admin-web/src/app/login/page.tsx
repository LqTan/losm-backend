import { Suspense } from "react";
import { LoginForm } from "@/presentation/components/features/auth";

export default function LoginPage() {
  return (
    <main id="auth">
      <Suspense fallback={null}>
        <LoginForm />
      </Suspense>
    </main>
  );
}
