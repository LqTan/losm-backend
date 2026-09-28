"use client";

import {
  Area,
  AreaChart,
  Bar,
  BarChart,
  CartesianGrid,
  Cell,
  Legend,
  Pie,
  PieChart,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from "recharts";
import type {
  DailyCount,
  GroupCount,
} from "@/domain/entities/dashboard.entity";
import { Card, CardBody, CardHeader, Spinner } from "@/presentation/components/ui";
import { formatDate } from "@/shared/utils/date";

const GENDER_COLORS = ["#4263eb", "#ae3ec9", "#0b7285", "#adb5bd"];
const STATUS_COLORS = ["#2f9e44", "#e03131"];

const TOOLTIP_STYLE = {
  borderRadius: 8,
  border: "1px solid #dee2e6",
  fontSize: 12,
} as const;

export function RegistrationsChart({ data }: { data: readonly DailyCount[] }) {
  return (
    <ResponsiveContainer width="100%" height={300}>
      <AreaChart data={data as DailyCount[]} margin={{ left: -20, right: 8 }}>
        <defs>
          <linearGradient id="registrationFill" x1="0" y1="0" x2="0" y2="1">
            <stop offset="0%" stopColor="#4263eb" stopOpacity={0.35} />
            <stop offset="100%" stopColor="#4263eb" stopOpacity={0} />
          </linearGradient>
        </defs>
        <CartesianGrid strokeDasharray="3 3" vertical={false} />
        <XAxis
          dataKey="date"
          tickFormatter={formatDate}
          tick={{ fontSize: 11 }}
          tickLine={false}
          axisLine={false}
          minTickGap={28}
        />
        <YAxis
          allowDecimals={false}
          tick={{ fontSize: 11 }}
          tickLine={false}
          axisLine={false}
        />
        <Tooltip
          contentStyle={TOOLTIP_STYLE}
          labelFormatter={(label) => formatDate(String(label))}
        />
        <Area
          type="monotone"
          dataKey="count"
          name="Registrations"
          stroke="#4263eb"
          fill="url(#registrationFill)"
          strokeWidth={2}
        />
      </AreaChart>
    </ResponsiveContainer>
  );
}

export function GenderChart({ data }: { data: readonly GroupCount[] }) {
  return (
    <ResponsiveContainer width="100%" height={260}>
      <PieChart>
        <Pie
          data={data as GroupCount[]}
          dataKey="count"
          nameKey="label"
          innerRadius={55}
          outerRadius={90}
          paddingAngle={2}
        >
          {data.map((entry, index) => (
            <Cell
              key={entry.label}
              fill={GENDER_COLORS[index % GENDER_COLORS.length]}
            />
          ))}
        </Pie>
        <Tooltip contentStyle={TOOLTIP_STYLE} />
        <Legend wrapperStyle={{ fontSize: 12 }} />
      </PieChart>
    </ResponsiveContainer>
  );
}

export function StatusChart({ data }: { data: readonly GroupCount[] }) {
  return (
    <ResponsiveContainer width="100%" height={260}>
      <BarChart data={data as GroupCount[]} margin={{ left: -20, right: 8 }}>
        <CartesianGrid strokeDasharray="3 3" vertical={false} />
        <XAxis
          dataKey="label"
          tick={{ fontSize: 11 }}
          tickLine={false}
          axisLine={false}
        />
        <YAxis
          allowDecimals={false}
          tick={{ fontSize: 11 }}
          tickLine={false}
          axisLine={false}
        />
        <Tooltip
          contentStyle={TOOLTIP_STYLE}
          cursor={{ fill: "rgba(134,142,150,0.15)" }}
        />
        <Bar dataKey="count" name="Customers" radius={[6, 6, 0, 0]}>
          {data.map((entry, index) => (
            <Cell
              key={entry.label}
              fill={STATUS_COLORS[index % STATUS_COLORS.length]}
            />
          ))}
        </Bar>
      </BarChart>
    </ResponsiveContainer>
  );
}

export function ChartCard({
  title,
  subtitle,
  isLoading,
  children,
}: {
  title: string;
  subtitle?: string;
  isLoading: boolean;
  children: React.ReactNode;
}) {
  return (
    <Card className="h-100">
      <CardHeader title={title} subtitle={subtitle} />
      <CardBody>
        {isLoading ? <Spinner /> : children}
      </CardBody>
    </Card>
  );
}
