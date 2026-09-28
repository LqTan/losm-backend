import type { DashboardRepository } from "@/application/abstractions/dashboard-repository.port";
import type { DashboardStats } from "@/domain/entities/dashboard.entity";
import type { DateRange } from "@/domain/value-objects/date-range.vo";
import { ValidationError } from "@/domain/errors/app-error";
import { isValidRange } from "@/domain/value-objects/date-range.vo";

export interface GetDashboardStatsUseCase {
  execute(range: DateRange): Promise<DashboardStats>;
}

export class GetDashboardStats implements GetDashboardStatsUseCase {
  constructor(private readonly dashboard: DashboardRepository) {}

  execute(range: DateRange): Promise<DashboardStats> {
    if (!isValidRange(range)) {
      throw new ValidationError("Invalid date range.");
    }
    return this.dashboard.getStats(range);
  }
}
