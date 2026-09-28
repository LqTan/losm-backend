"use client";

import type { User } from "@/domain/entities/user.entity";
import {
  Badge,
  GenderBadge,
  Modal,
  RoleBadge,
  StatusBadge,
} from "@/presentation/components/ui";
import { formatDateTime, initials } from "@/shared/utils/date";
import { cn } from "@/shared/utils/classnames";

export interface UserDetailDialogProps {
  user: User | null;
  onClose: () => void;
  onEdit?: (user: User) => void;
}

/**
 * Read-only user detail.
 *
 * Deliberately separate from the edit form: the list endpoint returns a
 * trimmed projection, so the detail view fetches the full record through
 * `getUserById` to show every field without duplicating the form markup.
 */
export function UserDetailDialog({
  user,
  onClose,
  onEdit,
}: UserDetailDialogProps) {
  return (
    <Modal
      open={user !== null}
      onClose={onClose}
      title="User details"
      size="lg"
      footer={
        <>
          <button
            type="button"
            className="btn btn-light"
            onClick={onClose}
          >
            Close
          </button>
          {onEdit && user !== null ? (
            <button
              type="button"
              className="btn btn-primary"
              onClick={() => onEdit(user)}
            >
              <i className="bi bi-pencil me-1" aria-hidden="true" />
              Edit user
            </button>
          ) : null}
        </>
      }
    >
      {user === null ? null : (
        <>
          <div className="d-flex align-items-center gap-3 mb-4 pb-3 border-bottom">
            <span
              className="user-circle"
              style={{ width: "3.25rem", height: "3.25rem", fontSize: "1rem" }}
              aria-hidden="true"
            >
              {initials(user.fullName || user.username)}
            </span>
            <div className="min-w-0">
              <p className="h5 mb-1 text-truncate">{user.fullName}</p>
              <p className="text-muted mb-0 text-truncate">
                {user.username} · {user.email}
              </p>
            </div>
            <div className="ms-auto d-flex gap-2 flex-shrink-0">
              <RoleBadge role={user.role} />
              <StatusBadge status={user.status} />
            </div>
          </div>

          <div className="row g-3">
            <DetailItem label="Full name" value={user.fullName} />
            <DetailItem label="Username" value={user.username} />
            <DetailItem label="Email" value={user.email} />
            <DetailItem
              label="Phone"
              value={user.phone ?? "—"}
            />
            <DetailItem
              label="Gender"
              value={<GenderBadge gender={user.gender} />}
            />
            <DetailItem
              label="Role"
              value={<RoleBadge role={user.role} />}
            />
            <DetailItem
              label="Status"
              value={<StatusBadge status={user.status} />}
            />
            <DetailItem
              label="Notify on new account"
              value={
                user.notifyOnAccountCreation ? (
                  <Badge tone="info">Enabled</Badge>
                ) : (
                  <Badge tone="light">Disabled</Badge>
                )
              }
            />
            <DetailItem
              label="Created at"
              value={formatDateTime(user.createdAt)}
            />
            <DetailItem
              label="Last login"
              value={formatDateTime(user.lastLoginAt)}
            />
            <DetailItem
              label="Changed at"
              value={formatDateTime(user.changedAt)}
            />
            <DetailItem label="User ID" value={<code>{user.id}</code>} />
          </div>
        </>
      )}
    </Modal>
  );
}

function DetailItem({
  label,
  value,
}: {
  label: string;
  value: React.ReactNode;
}) {
  return (
    <div className="col-12 col-sm-6">
      <div className="text-muted small mb-1">{label}</div>
      <div className={cn("fw-semibold text-break")}>{value}</div>
    </div>
  );
}
