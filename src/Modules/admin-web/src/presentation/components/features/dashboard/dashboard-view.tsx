"use client";

import { useState } from "react";
import { subDays } from "date-fns";
import type { DateRange } from "@/domain/value-objects/date-range.vo";
import { getContainer } from "@/infrastructure/container";
import { PageHeading, StatCard } from "@/presentation/components/global";
import {
  Alert,
  Badge,
  Card,
  CardBody,
  CardHeader,
  Grid,
  Spinner,
  Tbody,
  Td,
  Th,
  Thead,
  Tr,
  Table,
} from "@/presentation/components/ui";
import {
  ChartCard,
  GenderChart,
  RegistrationsChart,
  StatusChart,
} from "@/presentation/components/features/dashboard/charts";
import {
  DateRangeFilter,
  RefreshButton,
  defaultDateRange,
} from "@/presentation/components/features/dashboard/date-range-filter";
import { useAsync } from "@/presentation/hooks";
import { formatDateTime, toIsoDate } from "@/shared/utils/date";

/** Number of days in the active range, used as a short stat-card hint. */
function daysInRange(range: DateRange): number {
  const from = new Date(`${range.from}T00:00:00`).getTime();
  const to = new Date(`${range.to}T00:00:00`).getTime();
  return Math.round((to - from) / 86_400_000) + 1;
}

export function DashboardView() {
  const [range, setRange] = useState<DateRange>(defaultDateRange);
  const [presetDays, setPresetDays] = useState<number | null>(29);

  const { data, error, isLoading, reload } = useAsync(
    () => getContainer().usecases.dashboard.getStats.execute(range),
    [range.from, range.to],
  );

  function applyPreset(days: number) {
    setPresetDays(days);
    const today = new Date();
    setRange({
      from: toIsoDate(subDays(today, days)),
      to: toIsoDate(today),
    });
  }

  return (
    <>
      <PageHeading
        title="Dashboard"
        subtitle="Customer activity for the selected period."
        actions={
          <div className="d-flex align-items-end gap-2">
            <DateRangeFilter
              range={range}
              presetDays={presetDays}
              onPresetChange={applyPreset}
              onRangeChange={(next) => {
                setPresetDays(null);
                setRange(next);
              }}
            />
            <RefreshButton onClick={reload} loading={isLoading} />
          </div>
        }
      />

      <div className="page-content">
        {error !== null ? (
          <Alert tone="danger" title="Could not load dashboard">
            {error}
          </Alert>
        ) : null}

        {/* 6 thẻ: 1 cột trên mobile, 3 cột trên desktop (xem app-overrides.css). */}
        <Grid columns={3} className="mb-4">
          <StatCard
            label="Total customers"
            value={data?.totalCustomers ?? 0}
            icon="bi-people"
            tone="purple"
          />
          <StatCard
            label="Active customers"
            value={data?.activeCustomers ?? 0}
            icon="bi-person-check"
            tone="green"
          />
          <StatCard
            label="Blocked customers"
            value={data?.blockedCustomers ?? 0}
            icon="bi-person-x"
            tone="red"
          />
          <StatCard
            label="New in period"
            value={data?.newCustomers ?? 0}
            hint={`${daysInRange(range)} days`}
            icon="bi-person-plus"
            tone="blue"
          />
          <StatCard
            label="Signed in"
            value={data?.loggedInCustomers ?? 0}
            hint="during period"
            icon="bi-box-arrow-in-right"
            tone="orange"
          />
          <StatCard
            label="Administrators"
            value={data?.totalAdministrators ?? 0}
            icon="bi-shield-lock"
            tone="blue"
          />
        </Grid>

        <Card className="mb-4">
          <CardHeader
            title="New customer registrations"
            subtitle="Sign-ups per day between the selected dates."
          />
          <CardBody>
            {isLoading ? (
              <Spinner label="Loading chart…" />
            ) : (
              <RegistrationsChart data={data?.registrationsByDay ?? []} />
            )}
          </CardBody>
        </Card>

        <div className="row g-3 mb-4">
          <div className="col-12 col-lg-6">            <ChartCard
              title="Customers by gender"
              subtitle="All customers, current snapshot."
              isLoading={isLoading}
            >
              <GenderChart data={data?.byGender ?? []} />
            </ChartCard>
          </div>
          <div className="col-12 col-lg-6">
            <ChartCard
              title="Customers by status"
              subtitle="Active versus blocked accounts."
              isLoading={isLoading}
            >
              <StatusChart data={data?.byStatus ?? []} />
            </ChartCard>
          </div>
        </div>

        <Card>
          <CardHeader
            title="Most recent customers"
            subtitle="The 10 latest customer accounts."
          />
          <CardBody>
            <Table>
              <Thead>
                <Tr>
                  <Th>Full name</Th>
                  <Th>Username</Th>
                  <Th>Phone</Th>
                  <Th>Gender</Th>
                  <Th>Created</Th>
                  <Th>Last login</Th>
                </Tr>
              </Thead>
              <Tbody>
                {(data?.recentCustomers ?? []).map((customer) => (
                  <Tr key={customer.id}>
                    <Td className="fw-semibold">{customer.fullName}</Td>
                    <Td className="text-muted">{customer.username}</Td>
                    <Td>{customer.phone ?? "—"}</Td>
                    <Td>
                      {customer.gender !== null ? (
                        <Badge tone="info">{customer.gender}</Badge>
                      ) : (
                        <span className="text-muted">—</span>
                      )}
                    </Td>
                    <Td className="text-muted">
                      {formatDateTime(customer.createdAt)}
                    </Td>
                    <Td className="text-muted">
                      {formatDateTime(customer.lastLoginAt)}
                    </Td>
                  </Tr>
                ))}
              </Tbody>
            </Table>
            {!isLoading && (data?.recentCustomers.length ?? 0) === 0 ? (
              <p className="text-center text-muted py-4 mb-0">
                No customer data yet.
              </p>
            ) : null}
          </CardBody>
        </Card>
      </div>
    </>
  );
}
