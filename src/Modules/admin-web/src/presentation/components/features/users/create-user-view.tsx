"use client";

import { useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { toDisplayMessage } from "@/domain/errors/app-error";
import {
  parseUserScope,
  userScopePath,
  userScopeFilter,
} from "@/domain/value-objects/user-scope.vo";
import { getContainer } from "@/infrastructure/container";
import { PageHeading } from "@/presentation/components/global";
import { Button } from "@/presentation/components/ui";
import {
  UserForm,
  createEmptyUserFormValues,
  type UserFormValues,
} from "@/presentation/components/features/users/user-form";
import { useToast } from "@/presentation/hooks";

const SCOPE_COPY = {
  staff: {
    title: "New user",
    subtitle: "Create an account in the user management list.",
  },
  customer: {
    title: "New customer",
    subtitle: "Create a customer account.",
  },
} as const;

export function CreateUserView() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const toast = useToast();

  /**
   * The originating list is carried in the query string so the created account
   * goes back where the admin came from, and so the form knows whether the
   * role is already pinned by the scope.
   */
  const scope = parseUserScope(searchParams.get("scope"));
  const listPath = userScopePath(scope);
  const copy = SCOPE_COPY[scope];
  const pinnedRole = userScopeFilter(scope).role;

  const [values, setValues] = useState<UserFormValues>(() => ({
    ...createEmptyUserFormValues(),
    role: pinnedRole ?? "Customer",
  }));
  const [isSubmitting, setIsSubmitting] = useState(false);

  async function handleSubmit() {
    setIsSubmitting(true);
    try {
      // A scope that pins a role always wins over whatever the form holds, so
      // a customer can never be created from the customer form.
      const role = pinnedRole ?? values.role;

      await getContainer().usecases.users.create.execute({
        username: values.username,
        email: values.email,
        fullName: values.fullName,
        phone: values.phone || null,
        gender: values.gender,
        role,
        status: values.status,
        password: values.password,
        notify: values.notify,
      });

      toast.success(`${role} created.`, values.fullName);
      router.push(listPath);
    } catch (error) {
      toast.error("Unable to create user.", toDisplayMessage(error));
    } finally {
      setIsSubmitting(false);
    }
  }

  return (
    <>
      <PageHeading
        title={copy.title}
        subtitle={copy.subtitle}
        actions={
          <Button
            variant="light"
            icon="bi-arrow-left"
            onClick={() => router.push(listPath)}
          >
            Back
          </Button>
        }
      />

      <div className="page-content">
        <UserForm
          mode="create"
          scope={scope}
          values={values}
          isSubmitting={isSubmitting}
          onChange={setValues}
          onSubmit={() => void handleSubmit()}
          onCancel={() => router.push(listPath)}
        />
      </div>
    </>
  );
}
