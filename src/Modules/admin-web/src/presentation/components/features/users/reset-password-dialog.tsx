"use client";

import { useState } from "react";
import { toDisplayMessage } from "@/domain/errors/app-error";
import { getContainer } from "@/infrastructure/container";
import {
  Button,
  Field,
  Input,
  Modal,
} from "@/presentation/components/ui";
import type { UserListItem } from "@/domain/entities/user.entity";
import { useToast } from "@/presentation/hooks";
import { PASSWORD_HINT, validatePassword } from "@/shared/utils/validation";

interface FieldErrors {
  currentPassword?: string;
  newPassword?: string;
  confirmPassword?: string;
}

const EMPTY: FieldErrors = {};

/**
 * Dialog for resetting another user's password.
 *
 * Security requirement: the admin must confirm their own password before
 * changing someone else's, hence three fields — current (checked by the
 * backend), new (validated against the password policy) and confirm (matched client side).
 *
 * No "Notify" toggle: this operation always emails the affected user
 * because they need to know the new password.
 */
export function ResetPasswordDialog({
  user,
  onClose,
  onChanged,
}: {
  user: UserListItem | null;
  onClose: () => void;
  onChanged: () => void;
}) {
  const toast = useToast();

  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [errors, setErrors] = useState<FieldErrors>(EMPTY);
  const [isSaving, setIsSaving] = useState(false);

  function resetForm() {
    setCurrentPassword("");
    setNewPassword("");
    setConfirmPassword("");
    setErrors(EMPTY);
  }

  function handleClose() {
    if (isSaving) return;
    resetForm();
    onClose();
  }

  /** Validate on the client first so we never call the API with known-bad input. */
  function validate(): boolean {
    const next: FieldErrors = {};

    if (currentPassword.length === 0) {
      next.currentPassword = "Enter your current password.";
    }

    const passwordError = validatePassword(newPassword);
    if (passwordError) {
      next.newPassword = passwordError;
    } else if (newPassword === currentPassword) {
      next.newPassword = "The new password must be different.";
    }

    if (confirmPassword.length === 0) {
      next.confirmPassword = "Confirm the new password.";
    } else if (confirmPassword !== newPassword) {
      next.confirmPassword = "Passwords do not match.";
    }

    setErrors(next);
    return Object.keys(next).length === 0;
  }

  async function handleSubmit() {
    if (!user) return;
    if (!validate()) return;

    setIsSaving(true);
    try {
      const usecases = getContainer().usecases;

      const isCurrentPasswordValid =
        await usecases.auth.verifyCurrentPassword.execute(currentPassword);

      if (!isCurrentPasswordValid) {
        setErrors({ currentPassword: "Incorrect password." });
        return;
      }

      await usecases.users.resetPassword.execute(user.id, newPassword, true);

      toast.success("Password updated.", user.fullName);
      resetForm();
      onChanged();
      onClose();
    } catch (error) {
      toast.error("Unable to reset password.", toDisplayMessage(error));
    } finally {
      setIsSaving(false);
    }
  }

  return (
    <Modal
      open={user !== null}
      onClose={handleClose}
      title="Reset password"
      footer={
        <>
          <Button variant="light" onClick={handleClose} disabled={isSaving}>
            Cancel
          </Button>
          <Button
            onClick={() => void handleSubmit()}
            loading={isSaving}
            disabled={
              currentPassword.length === 0 ||
              newPassword.length === 0 ||
              confirmPassword.length === 0
            }
          >
            Update password
          </Button>
        </>
      }
    >
      <p className="text-muted">
        You are changing the password of <strong>{user?.fullName}</strong> (
        {user?.username}). This user will be notified by email.
      </p>

      <Field
        label="Your current password"
        htmlFor="reset-current-password"
        error={errors.currentPassword}
        hint="Required to confirm this sensitive action."
        spacing="md"
      >
        <Input
          id="reset-current-password"
          type="password"
          iconLeft="bi-shield-lock"
          autoComplete="current-password"
          value={currentPassword}
          invalid={Boolean(errors.currentPassword)}
          onChange={(event) => {
            setCurrentPassword(event.target.value);
            setErrors((current) => ({
              ...current,
              currentPassword: undefined,
            }));
          }}
        />
      </Field>

      <Field
        label="New password"
        htmlFor="reset-new-password"
        hint={PASSWORD_HINT}
        error={errors.newPassword}
        spacing="md"
      >
        <Input
          id="reset-new-password"
          type="password"
          iconLeft="bi-key"
          autoComplete="new-password"
          value={newPassword}
          invalid={Boolean(errors.newPassword)}
          onChange={(event) => {
            setNewPassword(event.target.value);
            setErrors((current) => ({
              ...current,
              newPassword: undefined,
              confirmPassword: undefined,
            }));
          }}
        />
      </Field>

      <Field
        label="Confirm new password"
        htmlFor="reset-confirm-password"
        error={errors.confirmPassword}
        spacing="none"
      >
        <Input
          id="reset-confirm-password"
          type="password"
          iconLeft="bi-check2-circle"
          autoComplete="new-password"
          value={confirmPassword}
          invalid={Boolean(errors.confirmPassword)}
          onChange={(event) => {
            setConfirmPassword(event.target.value);
            setErrors((current) => ({
              ...current,
              confirmPassword: undefined,
            }));
          }}
        />
      </Field>
    </Modal>
  );
}
