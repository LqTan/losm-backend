"use client";

import { useState } from "react";
import type { UserListItem } from "@/domain/entities/user.entity";
import { toDisplayMessage } from "@/domain/errors/app-error";
import { getContainer } from "@/infrastructure/container";
import {
  ConfirmDialog,
  GenderBadge,
  IconButton,
  RoleBadge,
  Spinner,
  StatusBadge,
  Tbody,
  Td,
  Th,
  Thead,
  Tr,
  Table,
} from "@/presentation/components/ui";
import { ResetPasswordDialog } from "@/presentation/components/features/users/reset-password-dialog";
import { useToast } from "@/presentation/hooks";
import { formatDateTime } from "@/shared/utils/date";

/** Column count, kept next to the header so the full-width rows cannot drift. */
const COLUMN_COUNT = 9;

export interface UserTableProps {
  users: readonly UserListItem[];
  isLoading: boolean;
  emptyMessage?: string;
  currentUserId?: string;
  onView: (user: UserListItem) => void;
  onEdit: (user: UserListItem) => void;
  onChanged: () => void;
}

export function UserTable({
  users,
  isLoading,
  emptyMessage = "No users match the current filters.",
  currentUserId,
  onView,
  onEdit,
  onChanged,
}: UserTableProps) {
  const [pendingDelete, setPendingDelete] = useState<UserListItem | null>(null);
  const [pendingReset, setPendingReset] = useState<UserListItem | null>(null);

  return (
    <>
      <Table>
        <Thead>
          <Tr>
            <Th>Full name</Th>
            <Th>Email</Th>
            <Th>Phone</Th>
            <Th>Gender</Th>
            <Th>Role</Th>
            <Th>Status</Th>
            <Th>Last login</Th>
            <Th>Changed</Th>
            <Th align="end">Actions</Th>
          </Tr>
        </Thead>
        <Tbody>
          {isLoading ? (
            <Tr>
              <Td align="center" colSpan={COLUMN_COUNT}>
                <div className="py-4">
                  <Spinner label="Loading users…" />
                </div>
              </Td>
            </Tr>
          ) : null}

          {!isLoading && users.length === 0 ? (
            <Tr>
              <Td align="center" colSpan={COLUMN_COUNT}>
                <div className="text-center py-4 text-muted">
                  <i
                    className="bi bi-inbox fs-2 d-block mb-2"
                    aria-hidden="true"
                  />
                  {emptyMessage}
                </div>
              </Td>
            </Tr>
          ) : null}

          {!isLoading &&
            users.map((user) => (
              <Tr key={user.id}>
                <Td>
                  {/* Username is folded into the name cell to keep the table
                      narrower and easier to scan. */}
                  <div className="fw-semibold">
                    {user.fullName}
                    {user.id === currentUserId ? (
                      <span className="text-muted small"> (you)</span>
                    ) : null}
                  </div>
                  <div className="text-muted small">{user.username}</div>
                </Td>
                <Td className="text-muted">
                  <div className="cell-truncate" title={user.email}>
                    {user.email}
                  </div>
                </Td>
                <Td>{user.phone ?? "—"}</Td>
                <Td>
                  <GenderBadge gender={user.gender} />
                </Td>
                <Td>
                  <RoleBadge role={user.role} />
                </Td>
                <Td>
                  <StatusBadge status={user.status} />
                </Td>
                <Td className="text-muted">
                  {formatDateTime(user.lastLoginAt)}
                </Td>
                <Td className="text-muted">
                  {formatDateTime(user.changedAt)}
                </Td>
                <Td align="end">
                  <div className="d-inline-flex gap-1">
                    <IconButton
                      icon="bi-eye"
                      label="View user"
                      tone="secondary"
                      onClick={() => onView(user)}
                    />
                    <IconButton
                      icon="bi-pencil"
                      label="Edit user"
                      tone="primary"
                      onClick={() => onEdit(user)}
                    />
                    <IconButton
                      icon="bi-key"
                      label="Reset password"
                      onClick={() => setPendingReset(user)}
                    />
                    <IconButton
                      icon="bi-trash2"
                      label="Delete user"
                      tone="danger"
                      disabled={user.id === currentUserId}
                      onClick={() => setPendingDelete(user)}
                    />
                  </div>
                </Td>
              </Tr>
            ))}
        </Tbody>
      </Table>

      <DeleteUserDialog
        user={pendingDelete}
        onClose={() => setPendingDelete(null)}
        onChanged={onChanged}
      />
      <ResetPasswordDialog
        user={pendingReset}
        onClose={() => setPendingReset(null)}
        onChanged={onChanged}
      />
    </>
  );
}

function DeleteUserDialog({
  user,
  onClose,
  onChanged,
}: {
  user: UserListItem | null;
  onClose: () => void;
  onChanged: () => void;
}) {
  const toast = useToast();
  const [isDeleting, setIsDeleting] = useState(false);

  async function handleConfirm() {
    if (!user) return;
    setIsDeleting(true);
    try {
      await getContainer().usecases.users.remove.execute(user.id);
      toast.success("User deleted.", user.fullName);
      onChanged();
      onClose();
    } catch (error) {
      toast.error("Unable to delete user.", toDisplayMessage(error));
    } finally {
      setIsDeleting(false);
    }
  }

  return (
    <ConfirmDialog
      open={user !== null}
      title="Delete user"
      confirmLabel="Delete"
      loading={isDeleting}
      onCancel={onClose}
      onConfirm={() => void handleConfirm()}
      message={
        user ? (
          <>
            Are you sure you want to delete <strong>{user.fullName}</strong> (
            {user.email})? This cannot be undone.
          </>
        ) : null
      }
    />
  );
}
