"use client";

import {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import { cn } from "@/shared/utils/classnames";

export type ToastTone = "success" | "danger" | "warning" | "info" | "primary";

export interface ToastItem {
  readonly id: number;
  readonly tone: ToastTone;
  readonly title: string;
  readonly description?: string;
}

export type ToastInput = Omit<ToastItem, "id">;

interface ToastContextValue {
  toasts: readonly ToastItem[];
  push: (toast: ToastInput) => void;
  success: (title: string, description?: string) => void;
  error: (title: string, description?: string) => void;
  dismiss: (id: number) => void;
}

const ToastContext = createContext<ToastContextValue | null>(null);

const AUTO_DISMISS_MS = 5000;

/**
 * Global toast store, replacing sonner.
 * The provider lives in app/providers so every screen shares one instance.
 */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<readonly ToastItem[]>([]);
  const nextId = useRef(1);

  const dismiss = useCallback((id: number) => {
    setToasts((current) => current.filter((toast) => toast.id !== id));
  }, []);

  const push = useCallback(
    (toast: ToastInput) => {
      const id = nextId.current;
      nextId.current += 1;
      setToasts((current) => [...current, { ...toast, id }]);

      if (typeof window !== "undefined") {
        window.setTimeout(() => dismiss(id), AUTO_DISMISS_MS);
      }
    },
    [dismiss],
  );

  const value = useMemo<ToastContextValue>(
    () => ({
      toasts,
      push,
      dismiss,
      success: (title, description) =>
        push({ tone: "success", title, description }),
      error: (title, description) =>
        push({ tone: "danger", title, description }),
    }),
    [toasts, push, dismiss],
  );

  return (
    <ToastContext.Provider value={value}>{children}</ToastContext.Provider>
  );
}

export function useToast(): ToastContextValue {
  const context = useContext(ToastContext);
  if (!context) {
    throw new Error("useToast must be used inside <ToastProvider>.");
  }
  return context;
}

const TONE_ICON: Record<ToastTone, string> = {
  success: "bi-check-circle-fill",
  danger: "bi-x-circle-fill",
  warning: "bi-exclamation-triangle-fill",
  info: "bi-info-circle-fill",
  primary: "bi-bell-fill",
};

/** Toast viewport, mounted in the root layout. */
export function ToastViewport() {
  const { toasts, dismiss } = useToast();

  if (toasts.length === 0) return null;

  return (
    <div
      className="toast-container position-fixed top-0 end-0 p-3"
      style={{ zIndex: 1090 }}
    >
      {toasts.map((toast) => (
        <div
          key={toast.id}
          className={cn(
            "toast show border-0 text-bg-" + toast.tone,
            "mb-2",
          )}
          role="alert"
          aria-live="assertive"
        >
          <div className="d-flex">
            <div className="toast-body d-flex align-items-start gap-2">
              <i
                className={cn("bi", TONE_ICON[toast.tone])}
                aria-hidden="true"
              />
              <div>
                <div className="fw-semibold">{toast.title}</div>
                {toast.description !== undefined ? (
                  <div className="small">{toast.description}</div>
                ) : null}
              </div>
            </div>
            <button
              type="button"
              className="btn-close btn-close-white me-2 m-auto"
              aria-label="Dismiss"
              onClick={() => dismiss(toast.id)}
            />
          </div>
        </div>
      ))}
    </div>
  );
}
