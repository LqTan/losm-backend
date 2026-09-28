/** A single data point of the daily registrations chart. */
export interface DailyCount {
  readonly date: string;
  readonly count: number;
}

/** A group used by the pie/bar charts (gender, status). */
export interface GroupCount {
  readonly label: string;
  readonly count: number;
}

export interface RecentCustomer {
  readonly id: string;
  readonly fullName: string;
  readonly username: string;
  readonly phone: string | null;
  readonly gender: string | null;
  readonly createdAt: string;
  readonly lastLoginAt: string | null;
}

/**
 * Dashboard metrics. Computed for role = Customer only;
 * the totals (total/active/blocked) cover all time, while the
 * "in period" figures (new/loggedIn) respect the date range.
 */
export interface DashboardStats {
  readonly from: string;
  readonly to: string;
  readonly totalCustomers: number;
  readonly activeCustomers: number;
  readonly blockedCustomers: number;
  readonly newCustomers: number;
  readonly loggedInCustomers: number;
  readonly totalAdministrators: number;
  readonly registrationsByDay: readonly DailyCount[];
  readonly byGender: readonly GroupCount[];
  readonly byStatus: readonly GroupCount[];
  readonly recentCustomers: readonly RecentCustomer[];
}
