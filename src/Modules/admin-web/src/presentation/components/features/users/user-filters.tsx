"use client";

import { useState } from "react";
import {
  USER_GENDERS,
  USER_STATUSES,
} from "@/domain/enums/user.enum";
import type { UserFilter } from "@/domain/value-objects/user-query.vo";
import type { UserListScope } from "@/domain/value-objects/user-scope.vo";
import { userScopeRoles } from "@/domain/value-objects/user-scope.vo";
import { Button, Field, Input, Select } from "@/presentation/components/ui";

const ALL = "";

const STATUS_OPTIONS = USER_STATUSES.map((status) => ({
  value: status,
  label: status,
}));
const GENDER_OPTIONS = USER_GENDERS.map((gender) => ({
  value: gender,
  label: gender,
}));

export const EMPTY_USER_FILTERS: UserFilter = {
  keyword: "",
  role: null,
  status: null,
  gender: null,
  createdFrom: "",
  createdTo: "",
};

export interface UserFiltersProps {
  filters: UserFilter;
  /** Active list scope; decides which roles the role filter may offer. */
  scope: UserListScope;
  onApply: (filters: UserFilter) => void;
}

/**
 * Filter bar for the user list.
 *
 * The role selector only offers the roles that exist inside the active scope
 * (see userScopeRoles), so it can never select a role that yields zero rows.
 *
 * Edits are held in a local draft and only pushed to the parent when the form
 * is submitted. Without this every keystroke would fire a request and reset
 * the pagination. The draft is re-synced from the props during render (React's
 * "adjust state when a prop changes" pattern) rather than in an effect, so the
 * external Reset button stays in sync too.
 *
 * The layout uses Bootstrap's `row` (which stretches by default) with each
 * field as a flex column. Every control therefore starts right below its
 * label and stays aligned even when only some fields have a hint.
 */
export function UserFilters({
  filters,
  scope,
  onApply,
}: UserFiltersProps) {
  const roleOptions = userScopeRoles(scope).map((role) => ({
    value: role,
    label: role,
  }));

  const [draft, setDraft] = useState<UserFilter>(filters);
  const [syncedFrom, setSyncedFrom] = useState<UserFilter>(filters);

  if (filters !== syncedFrom) {
    setSyncedFrom(filters);
    setDraft(filters);
  }

  function update(patch: Partial<UserFilter>) {
    setDraft((current) => ({ ...current, ...patch }));
  }

  return (
    <form
      className="card mb-4"
      onSubmit={(event) => {
        event.preventDefault();
        onApply({ ...draft });
      }}
    >
      <div className="card-body">
        <div className="row g-3 filter-grid">
          <div className="col-12 col-lg-4">
            <Field
              label="Keyword"
              htmlFor="filter-keyword"
              hint="Full name, username, email or phone."
              className="h-100"
            >
              <Input
                id="filter-keyword"
                iconLeft="bi-search"
                placeholder="Search users…"
                value={draft.keyword}
                onChange={(event) => update({ keyword: event.target.value })}
              />
            </Field>
          </div>

          <div className="col-6 col-lg-2">
            <Field label="Role" htmlFor="filter-role" className="h-100">
              <Select
                id="filter-role"
                options={roleOptions}
                placeholder="All roles"
                value={draft.role ?? ALL}
                onValueChange={(next) =>
                  update({ role: (next as UserFilter["role"]) ?? null })
                }
              />
            </Field>
          </div>

          <div className="col-6 col-lg-2">
            <Field label="Status" htmlFor="filter-status" className="h-100">
              <Select
                id="filter-status"
                options={STATUS_OPTIONS}
                placeholder="All statuses"
                value={draft.status ?? ALL}
                onValueChange={(next) =>
                  update({ status: (next as UserFilter["status"]) ?? null })
                }
              />
            </Field>
          </div>

          <div className="col-6 col-lg-2">
            <Field label="Gender" htmlFor="filter-gender" className="h-100">
              <Select
                id="filter-gender"
                options={GENDER_OPTIONS}
                placeholder="All genders"
                value={draft.gender ?? ALL}
                onValueChange={(next) =>
                  update({ gender: (next as UserFilter["gender"]) ?? null })
                }
              />
            </Field>
          </div>

          <div className="col-6 col-lg-2">
            <Field
              label="Created from"
              htmlFor="filter-from"
              className="h-100"
            >
              <Input
                id="filter-from"
                type="date"
                value={draft.createdFrom}
                onChange={(event) =>
                  update({ createdFrom: event.target.value })
                }
              />
            </Field>
          </div>

          <div className="col-6 col-lg-2">
            <Field label="Created to" htmlFor="filter-to" className="h-100">
              <Input
                id="filter-to"
                type="date"
                value={draft.createdTo}
                onChange={(event) => update({ createdTo: event.target.value })}
              />
            </Field>
          </div>
        </div>

        <div className="d-flex flex-wrap justify-content-end gap-2 mt-3">
          <Button
            type="button"
            variant="light"
            onClick={() => onApply({ ...EMPTY_USER_FILTERS })}
          >
            <i className="bi bi-x-circle me-1" aria-hidden="true" />
            Reset
          </Button>
          <Button type="submit">
            <i className="bi bi-funnel me-1" aria-hidden="true" />
            Apply filters
          </Button>
        </div>
      </div>
    </form>
  );
}
