import type {
  DashboardRepository,
  HttpClient,
} from "@/application/abstractions";
import type { DashboardStats } from "@/domain/entities/dashboard.entity";
import type { DateRange } from "@/domain/value-objects/date-range.vo";

export class RestDashboardRepository implements DashboardRepository {
  constructor(private readonly http: HttpClient) {}

  getStats(range: DateRange): Promise<DashboardStats> {
    return this.http
      .send<DashboardStats>({
        method: "GET",
        path: "/api/admin/dashboard/stats",
        query: { from: range.from, to: range.to },
      })
      .then((response) => response.body);
  }
}
