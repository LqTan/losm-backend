"use client";

import { useEffect, type ReactNode } from "react";
import { cn } from "@/shared/utils/classnames";
import { Button } from "@/presentation/components/ui/button";
import { useClickOutside } from "@/presentation/hooks";

/**
 * Bootstrap modal with the open state owned by React.
 *
 * Deliberately does NOT use bootstrap.bundle.js: data-bs-toggle conflicts
 * with React rendering and causes hydration errors.
 *
 * Two markup notes — both stem from bugs in the Bootstrap 5.0.0-beta1 build
 * bundled by the Mazer template:
 *
 * 1. `.modal-backdrop` must be a SIBLING of `.modal`, not a child. Nested,
 *    the backdrop (z-index 1040) paints over `.modal-dialog`
 *    (z-index auto) and greys out the entire dialog.
 * 2. This build sets `.modal-dialog { pointer-events: none }` but never
 *    restores it for `.modal-content` — a fix that only landed in the final
 *    Bootstrap 5 release). Without it nothing inside the dialog is clickable.
 *    Hence the `ls-modal` class plus the fixes in app-overrides.css.
 */
export function Modal({
  open,
  onClose,
  title,
  children,
  footer,
  size,
}: {
  open: boolean;
  onClose: () => void;
  title: string;
  children: ReactNode;
  footer?: ReactNode;
  size?: "sm" | "lg" | "xl";
}) {
  // The ref belongs on .modal-content, not the backdrop: the backdrop is a
  // SIBLING of .modal, so every click inside the dialog counts as "outside"
  // the backdrop and closes the modal — including clicks on the inputs.
  // Anchoring on .modal-content keeps clicks inside the dialog and only
  // closes when clicking the grey area (which belongs to .modal).
  const contentRef = useClickOutside<HTMLDivElement>(open, onClose);

  useEffect(() => {
    if (!open) return;

    function onKeyDown(event: KeyboardEvent) {
      if (event.key === "Escape") onClose();
    }

    document.addEventListener("keydown", onKeyDown);
    document.body.classList.add("modal-open");

    return () => {
      document.removeEventListener("keydown", onKeyDown);
      document.body.classList.remove("modal-open");
    };
  }, [open, onClose]);

  if (!open) return null;

  return (
    <>
      <div
        className="modal d-block ls-modal"
        role="dialog"
        aria-modal="true"
        aria-label={title}
      >
        <div
          className={cn(
            "modal-dialog modal-dialog-centered modal-dialog-scrollable",
            size && `modal-${size}`,
          )}
        >
        <div className="modal-content" ref={contentRef}>
          <div className="modal-header">
            <h5 className="modal-title">{title}</h5>
            <button
              type="button"
              className="btn-close"
              aria-label="Close"
              onClick={onClose}
            />
          </div>
          <div className="modal-body">{children}</div>
          {footer !== undefined ? (
            <div className="modal-footer">{footer}</div>
          ) : null}
          </div>
        </div>
      </div>

      <div className="modal-backdrop show ls-modal-backdrop" />
    </>
  );
}

export interface ConfirmDialogProps {
  open: boolean;
  title: string;
  message: ReactNode;
  confirmLabel?: string;
  cancelLabel?: string;
  loading?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/** Confirmation dialog shared by delete and password reset. */
export function ConfirmDialog({
  open,
  title,
  message,
  confirmLabel = "Confirm",
  cancelLabel = "Cancel",
  loading = false,
  onConfirm,
  onCancel,
}: ConfirmDialogProps) {
  return (
    <Modal
      open={open}
      onClose={onCancel}
      title={title}
      footer={
        <>
          <Button variant="light" onClick={onCancel} disabled={loading}>
            {cancelLabel}
          </Button>
          <Button variant="danger" onClick={onConfirm} loading={loading}>
            {confirmLabel}
          </Button>
        </>
      }
    >
      <div className="mb-0">{message}</div>
    </Modal>
  );
}
