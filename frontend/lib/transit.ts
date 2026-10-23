export type TransitDataset = { id: string; name: string; mode: "Bus" | "Metro"; scope: string; activeBatchId: string | null };
export type TransitBatch = {
  id: string; datasetId: string; status: string; fileNames: string; createdBy: string; createdAt: string;
  appliedAt: string | null; progress: number; routeCount: number; stopCount: number; patternCount: number;
  routeStopCount: number; errorCount: number; warningCount: number; summaryJson: string | null; failure: string | null; jobId: string | null;
};
export type ImportIssue = { id: number; severity: string; sheet: string; row: number; column: string; message: string };
export type ImportDetail = { batch: TransitBatch; issues: ImportIssue[] };
export type TransitRoute = { id: string; number: number; name: string; source: string; collectedDate: string };
export type TransitStop = { id: string; externalId: string; name: string; latitude: number; longitude: number; address: string; code?: string; kind?: string; parentExternalId?: string; isNotSafe: boolean; safetyReason?: string; patternId?: string; sequence?: number };
export type RouteDetail = {
  route: TransitRoute;
  patterns: { id: string; direction: number; patternCode: string; name: string }[];
  stops: TransitStop[];
  serviceWindows: { patternId: string; firstMinute: number | null; lastMinute: number | null; headwayMin: number | null; headwayMax: number | null; serviceDays: string }[];
  fares: { referenceAmount: number | null; currency: string; description: string }[];
};
export type Page<T> = { total: number; items: T[] };
export const formatRouteNumber = (number: number) => String(number).padStart(2, "0");
export const formatServiceTime = (minutes: number | null | undefined) => minutes == null ? "Chưa có" : `${String(Math.floor(minutes / 60)).padStart(2, "0")}:${String(minutes % 60).padStart(2, "0")}`;
export const importStatus: Record<string, string> = {
  Queued: "Chờ kiểm tra", Validating: "Đang kiểm tra", Ready: "Sẵn sàng áp dụng", Invalid: "Dữ liệu có lỗi",
  ValidationFailed: "Kiểm tra thất bại", ApplyQueued: "Chờ áp dụng", Applying: "Đang áp dụng", Applied: "Đã áp dụng", ApplyFailed: "Áp dụng thất bại",
};
export const isImportRunning = (status: string) => ["Queued", "Validating", "ApplyQueued", "Applying"].includes(status);
