"use client";

import { useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { Alert, Button } from "@/presentation/components/ui";
import { useAuth } from "@/presentation/hooks";

/**
 * Sign-in form, laid out after Mazer's auth-login.html page:
 * left column = form (white background), right column = background image (hidden on mobile).
 */
export function LoginForm() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { login, user, isLoading } = useAuth();

  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  const nextPath = searchParams.get("next") ?? "/admin/dashboard";

  async function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setError(null);
    setIsSubmitting(true);

    try {
      await login(email, password);
      router.replace(nextPath);
    } catch (caught) {
      setError(
        caught instanceof Error
          ? caught.message
          : "Unable to sign in. Please try again.",
      );
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <div className="row h-100 w-100">
      <div className="col-lg-5 col-12">
        <div id="auth-left">
          <div className="auth-logo">
            {/* eslint-disable-next-line @next/next/no-img-element -- static template logo, sized by the .auth-logo CSS rule. */}
            <img src="/assets/images/logo/logo.svg" alt="LocationSearch" />
          </div>

          <h1 className="auth-title">Log in.</h1>
          <p className="auth-subtitle mb-5">
            Sign in with your administrator account to continue.
          </p>

          {error !== null ? (
            <Alert tone="danger" className="mb-4">
              {error}
            </Alert>
          ) : null}

          {user && !isLoading ? (
            <Alert tone="info" className="mb-4">
              You are already signed in. Redirecting…
            </Alert>
          ) : null}

          <form onSubmit={handleSubmit} noValidate>
            <div className="form-group position-relative has-icon-left mb-4">
              <label className="visually-hidden" htmlFor="email">
                Email
              </label>
              <input
                id="email"
                name="email"
                type="email"
                className="form-control form-control-xl"
                autoComplete="username"
                placeholder="admin@locationsearch.local"
                value={email}
                onChange={(event) => setEmail(event.target.value)}
                disabled={isSubmitting}
                required
              />
              <div className="form-control-icon">
                <i className="bi bi-person" aria-hidden="true" />
              </div>
            </div>

            <div className="form-group position-relative has-icon-left mb-4">
              <label className="visually-hidden" htmlFor="password">
                Password
              </label>
              <input
                id="password"
                name="password"
                type="password"
                className="form-control form-control-xl"
                autoComplete="current-password"
                placeholder="Password"
                value={password}
                onChange={(event) => setPassword(event.target.value)}
                disabled={isSubmitting}
                required
              />
              <div className="form-control-icon">
                <i className="bi bi-shield-lock" aria-hidden="true" />
              </div>
            </div>

            <Button
              type="submit"
              size="lg"
              block
              loading={isSubmitting}
              disabled={isLoading}
            >
              {isSubmitting ? "Signing in…" : "Log in"}
            </Button>
          </form>
        </div>
      </div>

      <div className="col-lg-7 d-none d-lg-block">
        <div id="auth-right" />
      </div>
    </div>
  );
}
