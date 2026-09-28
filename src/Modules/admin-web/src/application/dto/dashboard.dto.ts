import type { DashboardStats } from "@/domain/entities/dashboard.entity";
import type { DateRange } from "@/domain/value-objects/date-range.vo";

export interface DashboardInput {
  readonly range: DateRange;
}

export type DashboardOutput = DashboardStats;
