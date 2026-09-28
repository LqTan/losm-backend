"use client";

import { useState } from "react";
import type {
  UserGender,
  UserRole,
  UserStatus,
} from "@/domain/enums/user.enum";
import {
  USER_GENDERS,
  USER_STATUSES,
} from "@/domain/enums/user.enum";
import {
  Button,
  Field,
  Input,
  Select,
  Switch,
} from "@/presentation/components/ui";
import type { UserListScope } from "@/domain/value-objects/user-scope.vo";
import {
  isRolePinnedByScope,
  userScopeRoles,
} from "@/domain/value-objects/user-scope.vo";
import {
  PASSWORD_HINT,
  PHONE_HINT,
  validateEmail,
  validateFullName,
  validatePassword,
  validatePhone,
  validateUsername,
} from "@/shared/utils/validation";
import { cn } from "@/shared/utils/classnames";

export interface UserFormValues {
  username: string;
  email: string;
  fullName: string;
  phone: string;
  gender: UserGender | null;
  role: UserRole;
  status: UserStatus;
  password: string;
  notify: boolean;
}

export function createEmptyUserFormValues(): UserFormValues {
  return {
    username: "",
    email: "",
    fullName: "",
    phone: "",
    gender: null,
    role: "Customer",
    status: "Active",
    password: "",
    notify: true,
  };
}

export function toUserFormValues(user: {
  username: string;
  email: string;
  fullName: string;
  phone: string | null;
  gender: UserGender | null;
  role: UserRole;
  status: UserStatus;
  notifyOnAccountCreation: boolean;
}): UserFormValues {
  return {
    username: user.username,
    email: user.email,
    fullName: user.fullName,
    phone: user.phone ?? "",
    gender: user.gender,
    role: user.role,
    status: user.status,
    password: "",
    notify: user.notifyOnAccountCreation,
  };
}

const STATUS_OPTIONS = USER_STATUSES.map((status) => ({
  value: status,
  label: status,
}));

const GENDER_OPTIONS = USER_GENDERS.map((gender) => ({
  value: gender,
  label: gender,
}));

export interface UserFormProps {
  mode: "create" | "edit";
  /**
   * List the form is being used from. It decides which roles may be picked:
   * when the scope pins a single role there is nothing to choose, so the
   * dropdown is replaced by a read-only value.
   */
  scope?: UserListScope;
  values: UserFormValues;
  isSubmitting: boolean;
  onChange: (values: UserFormValues) => void;
  onSubmit: () => void;
  onCancel: () => void;
}

/**
 * Form shared by the create and edit flows.
 * The parent owns the state (controlled) so a single form serves both
 * the create and the edit screen.
 */
export function UserForm({
  mode,
  scope = "staff",
  values,
  isSubmitting,
  onChange,
  onSubmit,
  onCancel,
}: UserFormProps) {
  const [fieldErrors, setFieldErrors] = useState<
    Partial<Record<keyof UserFormValues, string>>
  >({});

  function update<K extends keyof UserFormValues>(
    key: K,
    value: UserFormValues[K],
  ) {
    onChange({ ...values, [key]: value });
    setFieldErrors((current) => ({ ...current, [key]: undefined }));
  }

  /**
   * Runs the shared validators before hitting the API so the messages land on
   * the offending field instead of in a toast. The backend re-runs the exact
   * same rules, so nothing is trusted to the client.
   */
  function validate(): boolean {
    const next: Partial<Record<keyof UserFormValues, string>> = {};

    const fullNameError = validateFullName(values.fullName);
    if (fullNameError) next.fullName = fullNameError;

    const usernameError = validateUsername(values.username);
    if (usernameError) next.username = usernameError;

    const emailError = validateEmail(values.email);
    if (emailError) next.email = emailError;

    const phoneError = validatePhone(values.phone);
    if (phoneError) next.phone = phoneError;

    if (isCreate || values.password.length > 0) {
      const passwordError = validatePassword(values.password);
      if (passwordError) next.password = passwordError;
    }

    setFieldErrors(next);
    return Object.keys(next).length === 0;
  }

  function handleSubmit(event: React.FormEvent<HTMLFormElement>) {
    event.preventDefault();
    if (!validate()) return;
    onSubmit();
  }

  const isCreate = mode === "create";

  /**
   * The role picker always offers exactly the roles valid in this scope, so
   * the staff form never lets the admin pick Customer (an account created
   * that way would not appear in the list it was created from).
   *
   * Only a scope that pins one exact role (customer) has nothing left to
   * choose, so the picker becomes a read-only value there. Gender and Status
   * widen to half the row when it does, keeping the grid even.
   */
  const isRoleFixed = isRolePinnedByScope(scope);
  const roleOptions = userScopeRoles(scope).map((role) => ({
    value: role,
    label: role,
  }));

  return (
    <form onSubmit={handleSubmit} noValidate>
      <div className="card mb-4">
        <div className="card-body">
          <div className="row g-3 form-grid">
            <div className="col-md-6">
              <Field
                label="Full name"
                htmlFor="fullName"
                required
                error={fieldErrors.fullName}
              >
                <Input
                  id="fullName"
                  value={values.fullName}
                  maxLength={255}
                  placeholder="Nguyen Van A"
                  invalid={Boolean(fieldErrors.fullName)}
                  onChange={(event) => update("fullName", event.target.value)}
                  required
                />
              </Field>
            </div>

            <div className="col-md-6">
              <Field
                label="Username"
                htmlFor="username"
                required
                error={fieldErrors.username}
              >
                <Input
                  id="username"
                  value={values.username}
                  maxLength={100}
                  placeholder="nguyenvana"
                  invalid={Boolean(fieldErrors.username)}
                  onChange={(event) => update("username", event.target.value)}
                  required
                />
              </Field>
            </div>

            <div className="col-md-6">
              <Field
                label="Email"
                htmlFor="email"
                required
                error={fieldErrors.email}
              >
                <Input
                  id="email"
                  type="email"
                  value={values.email}
                  placeholder="user@example.com"
                  invalid={Boolean(fieldErrors.email)}
                  onChange={(event) => update("email", event.target.value)}
                  required
                />
              </Field>
            </div>

            <div className="col-md-6">
              <Field
                label="Phone"
                htmlFor="phone"
                hint={PHONE_HINT}
                error={fieldErrors.phone}
              >
                <Input
                  id="phone"
                  inputMode="numeric"
                  value={values.phone}
                  maxLength={15}
                  placeholder="84377000000"
                  invalid={Boolean(fieldErrors.phone)}
                  onChange={(event) =>
                    update("phone", event.target.value.replace(/\D/g, ""))
                  }
                />
              </Field>
            </div>

            <div className={cn(isRoleFixed ? "col-md-6" : "col-md-4")}>
              <Field label="Gender" htmlFor="gender">
                <Select
                  id="gender"
                  options={GENDER_OPTIONS}
                  placeholder="Not specified"
                  value={values.gender ?? ""}
                  onValueChange={(next) =>
                    update("gender", (next as UserGender | null) ?? null)
                  }
                />
              </Field>
            </div>

            {/*
              When the scope pins a single role there is nothing to pick, so
              the value is shown read-only instead of as a one-option
              dropdown. The role itself is still submitted by the parent view.
            */}
            {isRoleFixed ? (
              <div className={cn(isRoleFixed ? "col-md-6" : "col-md-4")}>
                <Field label="Role" htmlFor="role" required>
                  <div className="form-control-plain" id="role">
                    {values.role}
                  </div>
                </Field>
              </div>
            ) : (
              <div className="col-md-4">
                <Field label="Role" htmlFor="role" required>
                  <Select
                    id="role"
                    options={roleOptions}
                    value={values.role}
                    onValueChange={(next) =>
                      update("role", next as UserRole)
                    }
                  />
                </Field>
              </div>
            )}

            <div className={cn(isRoleFixed ? "col-md-6" : "col-md-4")}>
              <Field label="Status" htmlFor="status" required>
                <Select
                  id="status"
                  options={STATUS_OPTIONS}
                  value={values.status}
                  onValueChange={(next) =>
                    update("status", next as UserStatus)
                  }
                />
              </Field>
            </div>

            {/*
              The "Notify" switch is shown on the create form only: PUT
              /api/admin/users/{id} accepts `notify` but sends no email today,
              so rendering it in edit mode would be a control that does
              nothing. Changing the password (the key action in the user
              list) is the flow that actually emails the user.
            */}
            {isCreate ? (
              <div className="col-md-6">
                <Field
                  label="Password"
                  htmlFor="password"
                  required
                  hint={PASSWORD_HINT}
                  error={fieldErrors.password}
                >
                  <Input
                    id="password"
                    type="password"
                    iconLeft="bi-shield-lock"
                    autoComplete="new-password"
                    value={values.password}
                    invalid={Boolean(fieldErrors.password)}
                    onChange={(event) =>
                      update("password", event.target.value)
                    }
                    required
                  />
                </Field>
              </div>
            ) : null}
          </div>

          {/*
            Options block, always the last element in the form. It used to sit
            inside the grid before the password field, which pushed the
            password into the right half of its own row and left the layout
            ragged.
          */}
          {isCreate ? (
            <div className="form-options mt-4 pt-3 border-top">
              <Switch
                id="notify"
                checked={values.notify}
                onCheckedChange={(checked) => update("notify", checked)}
                label="Notify user of new account"
                description="Send an email with the username and password once the account is created."
              />
            </div>
          ) : null}
        </div>
      </div>

      <div className="d-flex justify-content-end gap-2">
        <Button variant="light" onClick={onCancel} disabled={isSubmitting}>
          Cancel
        </Button>
        <Button type="submit" loading={isSubmitting}>
          {isCreate ? "Create user" : "Save changes"}
        </Button>
      </div>
    </form>
  );
}
