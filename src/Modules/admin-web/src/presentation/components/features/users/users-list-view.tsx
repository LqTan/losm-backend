"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import {
  DEFAULT_PAGE_SIZE,
  PAGE_SIZE_OPTIONS,
} from "@/domain/value-objects/pagination.vo";
import type { UserFilter } from "@/domain/value-objects/user-query.vo";
import { getContainer } from "@/infrastructure/container";
import { toDisplayMessage } from "@/domain/errors/app-error";
import type { User, UserListItem } from "@/domain/entities/user.entity";
import { userScopeFilter } from "@/domain/value-objects/user-scope.vo";
import type { UserListScope } from "@/domain/value-objects/user-scope.vo";
import { PageHeading } from "@/presentation/components/global";
import {
  Alert,
  Button,
  Card,
  CardBody,
  CardHeader,
  Pagination,
} from "@/presentation/components/ui";
import { UserDetailDialog } from "@/presentation/components/features/users/user-detail-dialog";
import {
  EMPTY_USER_FILTERS,
  UserFilters,
} from "@/presentation/components/features/users/user-filters";
import { UserTable } from "@/presentation/components/features/users/user-table";
import { useAsync, useAuth, useToast } from "@/presentation/hooks";

/**
 * Per-scope copy. Kept next to the view because it is presentation-only —
 * the filter that defines each scope lives in domain/value-objects.
 */
const SCOPE_COPY: Record<
  UserListScope,
  { title: string; subtitle: string; empty: string }
> = {
  staff: {
    title: "User management",
    subtitle: "Administrator accounts and other non-customer accounts.",
    empty: "No administrator accounts yet.",
  },
  customer: {
    title: "Customers",
    subtitle: "Every customer account.",
    empty: "No customer accounts yet.",
  },
};

export function UsersListView({ scope }: { scope: UserListScope }) {
  const router = useRouter();
  const { user: currentUser } = useAuth();

  const copy = SCOPE_COPY[scope];

  const [filters, setFilters] = useState<UserFilter>(EMPTY_USER_FILTERS);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(DEFAULT_PAGE_SIZE);

  const { data, error, isLoading, reload } = useAsync(
    () =>
      getContainer().usecases.users.list.execute({
        ...filters,
        scope: userScopeFilter(scope),
        sortBy: "createdAt",
        sortDescending: true,
        pagination: { page, pageSize },
      }),
    [scope, filters, page, pageSize],
  );

  // Detail view needs the full record; the list endpoint only returns the
  // trimmed projection, so fetch it on demand.
  const [detailUser, setDetailUser] = useState<User | null>(null);
  const [isLoadingDetail, setIsLoadingDetail] = useState(false);

  const toast = useToast();

  async function handleView(user: UserListItem) {
    setIsLoadingDetail(true);
    try {
      setDetailUser(
        await getContainer().usecases.users.getById.execute(user.id),
      );
    } catch (caught) {
      toast.error("Unable to load user.", toDisplayMessage(caught));
    } finally {
      setIsLoadingDetail(false);
    }
  }

  // Log the list load error once instead of on every render.
  useEffect(() => {
    if (error) console.error("[users] load failed:", error);
  }, [error]);

  return (
    <>
      <PageHeading
        title={copy.title}
        subtitle={copy.subtitle}
        actions={
          <>
            <Button
              variant="light"
              icon="bi-arrow-clockwise"
              aria-label="Refresh"
              onClick={reload}
              loading={isLoading}
            />
            <Button
              icon="bi-plus"
              onClick={() => router.push("/admin/users/new")}
            >
              New user
            </Button>
          </>
        }
      />

      <div className="page-content">
        <UserFilters
          filters={filters}
          scope={scope}
          onApply={(next) => {
            setFilters(next);
            setPage(1);
          }}
        />

        {error !== null ? (
          <Alert tone="danger" title="Could not load users">
            {error}
          </Alert>
        ) : null}

        <Card>
          <CardHeader
            title="Accounts"
            subtitle={
              data
                ? `${data.totalCount} user${data.totalCount === 1 ? "" : "s"} found`
                : undefined
            }
          />
          <CardBody>
            <UserTable
              users={data?.items ?? []}
              isLoading={isLoading || isLoadingDetail}
              emptyMessage={copy.empty}
              currentUserId={currentUser?.id}
              onView={(user) => void handleView(user)}
              onEdit={(user) => router.push(`/admin/users/${user.id}`)}
              onChanged={reload}
            />

            <Pagination
              page={page}
              totalPages={data?.totalPages ?? 0}
              totalCount={data?.totalCount ?? 0}
              pageSize={pageSize}
              pageSizeOptions={PAGE_SIZE_OPTIONS}
              onPageChange={setPage}
              onPageSizeChange={(next) => {
                setPageSize(next);
                setPage(1);
              }}
            />
          </CardBody>
        </Card>
      </div>

      <UserDetailDialog
        user={detailUser}
        onClose={() => setDetailUser(null)}
        onEdit={(user) => {
          setDetailUser(null);
          router.push(`/admin/users/${user.id}`);
        }}
      />
    </>
  );
}
