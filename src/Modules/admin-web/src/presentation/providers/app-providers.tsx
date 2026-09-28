"use client";

import type { ReactNode } from "react";
import { AuthProvider, ToastProvider, ToastViewport } from "@/presentation/hooks";

/**
 * Composition root of the presentation layer.
 * Register any new global context or hook here, in one single place.
 */
export function AppProviders({ children }: { children: ReactNode }) {
  return (
    <ToastProvider>
      <AuthProvider>
        {children}
        <ToastViewport />
      </AuthProvider>
    </ToastProvider>
  );
}
