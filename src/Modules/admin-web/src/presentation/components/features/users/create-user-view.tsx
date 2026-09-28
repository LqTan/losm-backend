"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { toDisplayMessage } from "@/domain/errors/app-error";
import { getContainer } from "@/infrastructure/container";
import { PageHeading } from "@/presentation/components/global";
import { Button } from "@/presentation/components/ui";
import {
  UserForm,
  createEmptyUserFormValues,
  type UserFormValues,
} from "@/presentation/components/features/users/user-form";
import { useToast } from "@/presentation/hooks";

export function CreateUserView() {
  const router = useRouter();
  const toast = useToast();

  const [values, setValues] = useState<UserFormValues>(
    createEmptyUserFormValues(),
  );
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit() {
    setIsSubmitting(true);
    try {
      const usecases = getContainer().usecases.users;

      await usecases.create.execute({
        username: values.username,
        email: values.email,
        fullName: values.fullName,
        phone: values.phone || null,
        gender: values.gender,
        role: values.role,
        status: values.status,
        password: values.password,
        notify: values.notify,
      });

      toast.success("User created.", values.fullName);
      router.push("/admin/users");
    } catch (error) {
      toast.error("Unable to create user.", toDisplayMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <>
      <PageHeading
        title="New user"
        subtitle="Create an administrator or customer account."
        actions={
          <Button
            variant="light"
            icon="bi-arrow-left"
            onClick={() => router.push("/admin/users")}
          >
            Back to users
          </Button>
        }
      />

      <div className="page-content">
        <UserForm
          mode="create"
          values={values}
          isSubmitting={isSubmitting}
          onChange={setValues}
          onSubmit={() => void handleSubmit()}
          onCancel={() => router.push("/admin/users")}
        />
      </div>
    </>
  );
}
