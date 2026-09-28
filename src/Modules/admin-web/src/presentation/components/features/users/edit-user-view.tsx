"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { toDisplayMessage } from "@/domain/errors/app-error";
import { getContainer } from "@/infrastructure/container";
import { PageHeading } from "@/presentation/components/global";
import { Alert, Button, Spinner } from "@/presentation/components/ui";
import {
  UserForm,
  toUserFormValues,
  type UserFormValues,
} from "@/presentation/components/features/users/user-form";
import { useAsync, useToast } from "@/presentation/hooks";
import { formatDateTime } from "@/shared/utils/date";

export function EditUserView({ userId }: { userId: string }) {
  const router = useRouter();
  const toast = useToast();

  const { data: user, error, isLoading, reload } = useAsync(
    () => getContainer().usecases.users.getById.execute(userId),
    [userId],
  );

  const [values, setValues] = useState<UserFormValues | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);

  // Seed the form once the user record has loaded.
  const formValues = user
    ? (values ?? toUserFormValues({
        username: user.username,
        email: user.email,
        fullName: user.fullName,
        phone: user.phone,
        gender: user.gender,
        role: user.role,
        status: user.status,
        notifyOnAccountCreation: user.notifyOnAccountCreation,
      }))
    : null;

  async function handleSubmit() {
    if (!formValues) return;
    setIsSubmitting(true);

    try {
      await getContainer().usecases.users.update.execute(userId, {
        username: formValues.username,
        email: formValues.email,
        fullName: formValues.fullName,
        phone: formValues.phone || null,
        gender: formValues.gender,
        role: formValues.role,
        status: formValues.status,
        notify: formValues.notify,
      });

      toast.success("User updated.", formValues.fullName);
      router.push("/admin/users");
    } catch (error) {
      toast.error("Unable to update user.", toDisplayMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  }

  if (isLoading) {
    return <Spinner label="Loading user…" />;
  }

  if (error !== null || user === null || formValues === null) {
    return (
      <>
        <PageHeading title="User not found" />
        <div className="page-content">
          <Alert tone="danger" title="Could not load user">
            {error ?? "This user does not exist."}
          </Alert>
          <Button
            variant="light"
            icon="bi-arrow-left"
            onClick={() => router.push("/admin/users")}
          >
            Back to users
          </Button>
        </div>
      </>
    );
  }

  return (
    <>
      <PageHeading
        title={user.fullName}
        subtitle={`${user.email} · Created ${formatDateTime(user.createdAt)} · Last login ${formatDateTime(user.lastLoginAt)}`}
        actions={
          <>
            <Button
              variant="light"
              icon="bi-arrow-clockwise"
              aria-label="Reload"
              onClick={reload}
            />
            <Button
              variant="light"
              icon="bi-arrow-left"
              onClick={() => router.push("/admin/users")}
            >
              Back to users
            </Button>
          </>
        }
      />

      <div className="page-content">
        <UserForm
          mode="edit"
          values={formValues}
          isSubmitting={isSubmitting}
          onChange={setValues}
          onSubmit={() => void handleSubmit()}
          onCancel={() => router.push("/admin/users")}
        />
      </div>
    </>
  );
}
