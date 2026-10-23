"use client";

import { useCallback, useEffect, useRef, useState } from "react";
import dynamic from "next/dynamic";
import {
  Bus,
  TrainFront,
  Upload,
  FileUp,
  RefreshCw,
  Search,
  X,
  Loader2,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Eye,
  MapPin,
} from "lucide-react";
import { toast } from "sonner";
import { api } from "@/lib/api";
import { getToken } from "@/lib/auth";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import {
  formatRouteNumber,
  formatServiceTime,
  type TransitDataset,
  type TransitBatch,
  type TransitRoute,
  type TransitStop,
  type RouteDetail,
  type Page,
} from "@/lib/transit";

const TransitRouteMap = dynamic(
  () => import("@/components/admin/transit/TransitRouteMap"),
  {
    ssr: false,
    loading: () => (
      <div className="flex h-[300px] items-center justify-center rounded-lg border bg-muted/20 text-xs text-muted-foreground">
        Đang tải bản đồ…
      </div>
    ),
  }
);

function getPageNumbers(current: number, total: number): (number | string)[] {
  if (total <= 1) return [1];
  if (total <= 7) {
    return Array.from({ length: total }, (_, i) => i + 1);
  }
  const pages: (number | string)[] = [];
  if (current <= 4) {
    for (let i = 1; i <= 5; i++) pages.push(i);
    pages.push("...");
    pages.push(total);
  } else if (current >= total - 3) {
    pages.push(1);
    pages.push("...");
    for (let i = total - 4; i <= total; i++) pages.push(i);
  } else {
    pages.push(1);
    pages.push("...");
    pages.push(current - 1);
    pages.push(current);
    pages.push(current + 1);
    pages.push("...");
    pages.push(total);
  }
  return pages;
}

export default function TransitAdminPage() {
  const [mode, setMode] = useState<"Bus" | "Metro">("Bus");
  const [view, setView] = useState<"routes" | "stops">("routes");
  const [network, setNetwork] = useState<TransitDataset | null>(null);

  // List data
  const [routes, setRoutes] = useState<TransitRoute[]>([]);
  const [stops, setStops] = useState<TransitStop[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [loading, setLoading] = useState(false);

  // Pagination & filter
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(15);
  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");

  // Import states
  const [file, setFile] = useState<File | null>(null);
  const [importing, setImporting] = useState(false);
  const fileInputRef = useRef<HTMLInputElement>(null);

  // Route detail modal
  const [routeDetail, setRouteDetail] = useState<RouteDetail | null>(null);
  const [patternId, setPatternId] = useState("");
  const [loadingDetail, setLoadingDetail] = useState(false);

  const datasetId = network?.mode === mode ? network.id : "";

  // Load network dataset info
  const loadNetwork = useCallback(async () => {
    try {
      const res = await api.get<{ dataset: TransitDataset | null }>(
        `/api/admin/transit/network?mode=${mode}`,
        getToken()
      );
      setNetwork(res.dataset);
      return res.dataset;
    } catch {
      return null;
    }
  }, [mode]);

  // Load table data
  const loadData = useCallback(
    async (dsId?: string, curPage = page, curSearch = search, curSize = pageSize) => {
      const targetDatasetId = dsId ?? datasetId;
      if (!targetDatasetId) {
        setRoutes([]);
        setStops([]);
        setTotalCount(0);
        return;
      }
      setLoading(true);
      try {
        const token = getToken();
        if (view === "routes") {
          const res = await api.get<Page<TransitRoute>>(
            `/api/admin/transit/datasets/${targetDatasetId}/routes?page=${curPage}&pageSize=${curSize}&search=${encodeURIComponent(
              curSearch
            )}`,
            token
          );
          setRoutes(res.items);
          setTotalCount(res.total);
        } else {
          const res = await api.get<Page<TransitStop>>(
            `/api/admin/transit/datasets/${targetDatasetId}/stops?page=${curPage}&pageSize=${curSize}&search=${encodeURIComponent(
              curSearch
            )}`,
            token
          );
          setStops(res.items);
          setTotalCount(res.total);
        }
      } catch (err: unknown) {
        const msg = err instanceof Error ? err.message : "Không thể tải dữ liệu.";
        toast.error(msg);
      } finally {
        setLoading(false);
      }
    },
    [datasetId, view, page, search, pageSize]
  );

  // Initialize and reload when mode/view/page/search/pageSize changes
  useEffect(() => {
    let active = true;
    void (async () => {
      const ds = await loadNetwork();
      if (!active) return;
      if (ds?.id) {
        await loadData(ds.id, page, search, pageSize);
      } else {
        setRoutes([]);
        setStops([]);
        setTotalCount(0);
      }
    })();
    return () => {
      active = false;
    };
  }, [mode, view, page, search, pageSize, loadNetwork, loadData]);

  // Handle Search Form
  const handleSearchSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    setSearch(searchInput.trim());
    setPage(1);
  };

  const handleResetSearch = () => {
    setSearchInput("");
    setSearch("");
    setPage(1);
  };

  // One-click Import Handler
  const handleImport = async () => {
    if (!file) {
      fileInputRef.current?.click();
      return;
    }

    setImporting(true);
    try {
      const form = new FormData();
      form.append("files", file);

      // 1. Upload file
      const initialBatch = await api.upload<TransitBatch>(
        `/api/admin/transit/imports?mode=${mode}`,
        form,
        getToken()
      );

      // 2. Poll until batch is validated (Ready / Invalid / ValidationFailed)
      let batch = initialBatch;
      const maxPoll = 40;
      for (let i = 0; i < maxPoll; i++) {
        await new Promise((r) => setTimeout(r, 600));
        const res = await api.get<{ batch: TransitBatch; issues: unknown[] }>(
          `/api/admin/transit/imports/${batch.id}`,
          getToken()
        );
        batch = res.batch;
        if (["Ready", "Invalid", "ValidationFailed"].includes(batch.status)) {
          break;
        }
      }

      if (batch.status !== "Ready") {
        const errorMsg =
          batch.failure ||
          (batch.errorCount > 0
            ? `Dữ liệu có ${batch.errorCount} lỗi, không thể import.`
            : "Kiểm tra file thất bại.");
        throw new Error(errorMsg);
      }

      // 3. Apply validated batch immediately
      await api.post(
        `/api/admin/transit/imports/${batch.id}/apply`,
        { confirmFullReplacement: true },
        getToken()
      );

      // 4. Poll until batch is applied (Applied / ApplyFailed)
      for (let i = 0; i < maxPoll; i++) {
        await new Promise((r) => setTimeout(r, 600));
        const res = await api.get<{ batch: TransitBatch; issues: unknown[] }>(
          `/api/admin/transit/imports/${batch.id}`,
          getToken()
        );
        batch = res.batch;
        if (["Applied", "ApplyFailed"].includes(batch.status)) {
          break;
        }
      }

      if (batch.status !== "Applied") {
        throw new Error(batch.failure || "Áp dụng dữ liệu thất bại.");
      }

      toast.success(
        `Import thành công! Đã nạp ${batch.routeCount} tuyến và ${batch.stopCount} trạm.`
      );
      setFile(null);
      if (fileInputRef.current) fileInputRef.current.value = "";

      const updatedDs = await loadNetwork();
      if (updatedDs?.id) {
        setPage(1);
        await loadData(updatedDs.id, 1, search, pageSize);
      }
    } catch (err: unknown) {
      const msg = err instanceof Error ? err.message : "Có lỗi xảy ra khi import.";
      toast.error(msg);
    } finally {
      setImporting(false);
    }
  };

  // Open Route Detail Dialog
  const openRouteDetail = async (id: string) => {
    setLoadingDetail(true);
    try {
      const data = await api.get<RouteDetail>(
        `/api/admin/transit/routes/${id}`,
        getToken()
      );
      setRouteDetail(data);
      setPatternId(data.patterns[0]?.id ?? "");
    } catch (err: unknown) {
      toast.error(err instanceof Error ? err.message : "Không thể tải chi tiết tuyến.");
    } finally {
      setLoadingDetail(false);
    }
  };

  const totalPages = Math.max(1, Math.ceil(totalCount / pageSize));
  const activeStops =
    routeDetail?.stops
      .filter((s) => s.patternId === patternId)
      .sort((a, b) => (a.sequence ?? 0) - (b.sequence ?? 0)) ?? [];
  const activeService = routeDetail?.serviceWindows.find(
    (s) => s.patternId === patternId
  );

  return (
    <div className="space-y-4 p-4 md:p-6">
      {/* Page Header matching Admin Format */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h1 className="text-xl font-bold tracking-tight">Public Transit</h1>
          <p className="text-xs text-muted-foreground">
            Quản lý mạng lưới tuyến và trạm xe buýt / metro
          </p>
        </div>

        {/* Action bar: Upload & Import buttons */}
        <div className="flex flex-wrap items-center gap-2">
          <input
            ref={fileInputRef}
            type="file"
            accept=".xlsx,.csv"
            multiple
            className="hidden"
            onChange={(e) => {
              const f = e.target.files?.[0] ?? null;
              setFile(f);
            }}
          />

          <Button
            variant="outline"
            size="sm"
            disabled={importing}
            onClick={() => fileInputRef.current?.click()}
            className="gap-1.5"
          >
            <Upload className="h-3.5 w-3.5" />
            <span className="max-w-[140px] truncate">
              {file ? file.name : "Chọn file"}
            </span>
          </Button>

          {file && (
            <Button
              variant="ghost"
              size="sm"
              disabled={importing}
              onClick={() => {
                setFile(null);
                if (fileInputRef.current) fileInputRef.current.value = "";
              }}
              className="h-8 px-2 text-muted-foreground"
              title="Bỏ chọn"
            >
              <X className="h-3.5 w-3.5" />
            </Button>
          )}

          <Button
            size="sm"
            disabled={importing}
            onClick={handleImport}
            className="gap-1.5"
          >
            {importing ? (
              <Loader2 className="h-3.5 w-3.5 animate-spin" />
            ) : (
              <FileUp className="h-3.5 w-3.5" />
            )}
            <span>{importing ? "Đang import..." : "Import"}</span>
          </Button>

          <Button
            variant="outline"
            size="sm"
            onClick={() => {
              void loadNetwork();
              void loadData();
            }}
            disabled={loading || importing}
            className="gap-1.5"
          >
            <RefreshCw
              className={`h-3.5 w-3.5 ${loading ? "animate-spin" : ""}`}
            />
            <span>Làm mới</span>
          </Button>
        </div>
      </div>

      {/* Filter and Search Bar Card */}
      <Card>
        <CardContent className="pt-5">
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center">
            {/* Search Input */}
            <form
              onSubmit={handleSearchSubmit}
              className="relative flex-1"
            >
              <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                type="text"
                placeholder={
                  view === "routes"
                    ? "Tìm theo số tuyến, tên tuyến..."
                    : "Tìm theo mã trạm, tên, địa chỉ..."
                }
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                className="pl-9 text-sm"
              />
            </form>

            {/* Filter Dropdowns */}
            <div className="flex flex-wrap items-center gap-2">
              <Button
                type="button"
                size="sm"
                variant="secondary"
                onClick={handleSearchSubmit}
              >
                Tìm
              </Button>

              {search && (
                <Button
                  type="button"
                  size="sm"
                  variant="ghost"
                  onClick={handleResetSearch}
                  className="h-9 px-2.5 text-xs text-muted-foreground hover:text-foreground"
                >
                  <X className="mr-1 h-3.5 w-3.5" />
                  Xóa lọc
                </Button>
              )}

              {/* Mode Select */}
              <select
                value={mode}
                onChange={(e) => {
                  setMode(e.target.value as "Bus" | "Metro");
                  setPage(1);
                  setSearch("");
                  setSearchInput("");
                }}
                className="h-9 rounded-md border border-input bg-background px-3 py-1 text-xs shadow-xs focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="Bus">Xe buýt (Bus)</option>
                <option value="Metro">Metro</option>
              </select>

              {/* View Select */}
              <select
                value={view}
                onChange={(e) => {
                  setView(e.target.value as "routes" | "stops");
                  setPage(1);
                  setSearch("");
                  setSearchInput("");
                }}
                className="h-9 rounded-md border border-input bg-background px-3 py-1 text-xs shadow-xs focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="routes">Tuyến xe</option>
                <option value="stops">Trạm dừng</option>
              </select>

              {/* Page Size Select */}
              <select
                value={pageSize}
                onChange={(e) => {
                  setPageSize(Number(e.target.value));
                  setPage(1);
                }}
                className="h-9 rounded-md border border-input bg-background px-2.5 py-1 text-xs shadow-xs focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value={15}>15 / trang</option>
                <option value={25}>25 / trang</option>
                <option value={50}>50 / trang</option>
                <option value={100}>100 / trang</option>
              </select>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Data Table Card */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between pb-3">
          <div>
            <CardTitle className="text-base font-semibold">
              {view === "routes" ? "Danh sách tuyến xe" : "Danh sách trạm dừng"}
            </CardTitle>
            <CardDescription className="text-xs">
              Tổng cộng {totalCount.toLocaleString()}{" "}
              {view === "routes" ? "tuyến" : "trạm"}
              {search ? " (đang lọc)" : ""}
            </CardDescription>
          </div>
        </CardHeader>

        <CardContent className="p-0">
          <div className="overflow-x-auto min-h-[360px]">
            <Table>
              <TableHeader className="bg-muted/50">
                {view === "routes" ? (
                  <TableRow>
                    <TableHead className="w-[100px]">Số tuyến</TableHead>
                    <TableHead>Tên tuyến</TableHead>
                    <TableHead className="w-[240px]">Nguồn</TableHead>
                    <TableHead className="w-[140px]">Ngày thu thập</TableHead>
                    <TableHead className="w-[130px] text-right">Thao tác</TableHead>
                  </TableRow>
                ) : (
                  <TableRow>
                    <TableHead className="w-[120px]">Mã trạm</TableHead>
                    <TableHead className="min-w-[220px]">Tên trạm</TableHead>
                    <TableHead className="min-w-[260px]">Địa chỉ</TableHead>
                    <TableHead className="w-[180px]">Tọa độ (Lat, Lng)</TableHead>
                    <TableHead className="w-[110px]">Loại</TableHead>
                    <TableHead className="w-[130px] text-right">Trạng thái</TableHead>
                  </TableRow>
                )}
              </TableHeader>

              <TableBody>
                {loading ? (
                  <TableRow>
                    <TableCell
                      colSpan={view === "routes" ? 5 : 6}
                      className="h-32 text-center"
                    >
                      <div className="flex flex-col items-center justify-center gap-2 text-muted-foreground">
                        <Loader2 className="h-6 w-6 animate-spin text-primary" />
                        <span className="text-xs">Đang tải dữ liệu...</span>
                      </div>
                    </TableCell>
                  </TableRow>
                ) : (view === "routes" ? routes.length === 0 : stops.length === 0) ? (
                  <TableRow>
                    <TableCell
                      colSpan={view === "routes" ? 5 : 6}
                      className="h-32 text-center"
                    >
                      <div className="flex flex-col items-center justify-center gap-2 text-muted-foreground">
                        <Bus className="h-8 w-8 stroke-[1.5]" />
                        <span className="text-sm font-medium">
                          Không tìm thấy dữ liệu
                        </span>
                        <span className="text-xs">
                          {search
                            ? "Không có kết quả khớp với từ khóa tìm kiếm."
                            : "Vui lòng chọn file và bấm Import để nạp dữ liệu mạng tuyến."}
                        </span>
                      </div>
                    </TableCell>
                  </TableRow>
                ) : view === "routes" ? (
                  routes.map((r) => (
                    <TableRow key={r.id}>
                      <TableCell>
                        <Badge variant="outline" className="font-bold">
                          {formatRouteNumber(r.number)}
                        </Badge>
                      </TableCell>
                      <TableCell className="font-medium">{r.name}</TableCell>
                      <TableCell className="text-xs text-muted-foreground truncate max-w-[240px]">
                        {r.source || "—"}
                      </TableCell>
                      <TableCell className="text-xs text-muted-foreground">
                        {r.collectedDate || "—"}
                      </TableCell>
                      <TableCell className="text-right">
                        <Button
                          size="sm"
                          variant="outline"
                          onClick={() => openRouteDetail(r.id)}
                          className="h-8 gap-1 px-2.5 text-xs"
                          disabled={loadingDetail}
                        >
                          <Eye className="h-3.5 w-3.5" />
                          <span>Chi tiết</span>
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))
                ) : (
                  stops.map((s) => (
                    <TableRow key={s.id}>
                      <TableCell className="font-mono text-xs">
                        {s.externalId}
                      </TableCell>
                      <TableCell className="font-medium">
                        <div>{s.name}</div>
                        {s.parentExternalId && (
                          <span className="text-xs text-muted-foreground">
                            Thuộc ga {s.parentExternalId}
                          </span>
                        )}
                      </TableCell>
                      <TableCell className="text-xs text-muted-foreground">
                        {s.address || "—"}
                      </TableCell>
                      <TableCell className="font-mono text-xs text-muted-foreground">
                        {s.latitude.toFixed(5)}, {s.longitude.toFixed(5)}
                      </TableCell>
                      <TableCell>
                        <Badge variant="secondary" className="text-xs">
                          {s.kind || "Stop"}
                        </Badge>
                      </TableCell>
                      <TableCell className="text-right">
                        {s.isNotSafe ? (
                          <Badge variant="destructive" className="text-xs">
                            Sai tọa độ
                          </Badge>
                        ) : (
                          <Badge
                            variant="outline"
                            className="text-xs text-green-700 border-green-300"
                          >
                            Hợp lệ
                          </Badge>
                        )}
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>

          {/* Standard Pagination Footer matching Places & Users */}
          <div className="flex flex-col items-center justify-between gap-3 border-t px-4 py-3 sm:flex-row">
            <div className="text-xs text-muted-foreground">
              {totalCount > 0
                ? `Hiển thị ${(page - 1) * pageSize + 1} - ${Math.min(
                    page * pageSize,
                    totalCount
                  )} trong tổng số ${totalCount.toLocaleString()} bản ghi`
                : "0 bản ghi"}
            </div>

            <div className="flex items-center gap-1">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage(1)}
                disabled={page <= 1 || loading}
                className="h-8 w-8 p-0 text-xs"
                title="Trang đầu"
              >
                <ChevronsLeft className="h-4 w-4" />
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page <= 1 || loading}
                className="h-8 w-8 p-0 text-xs"
                title="Trang trước"
              >
                <ChevronLeft className="h-4 w-4" />
              </Button>

              {getPageNumbers(page, totalPages).map((pNum, idx) =>
                pNum === "..." ? (
                  <span
                    key={`ellipsis-${idx}`}
                    className="px-1 text-xs text-muted-foreground select-none"
                  >
                    …
                  </span>
                ) : (
                  <Button
                    key={`page-${pNum}`}
                    variant={page === pNum ? "default" : "outline"}
                    size="sm"
                    onClick={() => setPage(pNum as number)}
                    disabled={loading}
                    className={`h-8 min-w-[32px] px-2 text-xs font-medium ${
                      page === pNum ? "pointer-events-none" : ""
                    }`}
                  >
                    {pNum}
                  </Button>
                )
              )}

              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage((p) => Math.min(totalPages, p + 1))}
                disabled={page >= totalPages || loading}
                className="h-8 w-8 p-0 text-xs"
                title="Trang sau"
              >
                <ChevronRight className="h-4 w-4" />
              </Button>
              <Button
                variant="outline"
                size="sm"
                onClick={() => setPage(totalPages)}
                disabled={page >= totalPages || loading}
                className="h-8 w-8 p-0 text-xs"
                title="Trang cuối"
              >
                <ChevronsRight className="h-4 w-4" />
              </Button>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Clean Route Detail Dialog */}
      <Dialog
        open={!!routeDetail}
        onOpenChange={(open) => {
          if (!open) setRouteDetail(null);
        }}
      >
        <DialogContent className="max-h-[90vh] overflow-y-auto sm:max-w-4xl">
          <DialogHeader>
            <DialogTitle>
              {routeDetail &&
                `Tuyến ${formatRouteNumber(routeDetail.route.number)} — ${
                  routeDetail.route.name
                }`}
            </DialogTitle>
          </DialogHeader>

          {routeDetail && (
            <div className="space-y-4 pt-2">
              <div className="flex flex-col gap-2 sm:flex-row sm:items-center sm:justify-between">
                <select
                  aria-label="Chiều hành trình"
                  className="h-9 rounded-md border bg-background px-3 text-sm flex-1"
                  value={patternId}
                  onChange={(e) => setPatternId(e.target.value)}
                >
                  {routeDetail.patterns.map((p) => (
                    <option key={p.id} value={p.id}>
                      {p.name} · Chiều {p.direction}
                      {p.patternCode === "default"
                        ? ""
                        : ` · Nhánh ${p.patternCode}`}
                    </option>
                  ))}
                </select>
              </div>

              <div className="rounded-md bg-muted/40 p-3 text-xs space-y-1">
                <p>
                  <span className="font-semibold">Giờ hoạt động:</span>{" "}
                  {formatServiceTime(activeService?.firstMinute)} –{" "}
                  {formatServiceTime(activeService?.lastMinute)} ·{" "}
                  <span className="font-semibold">Giãn chuyến:</span>{" "}
                  {activeService?.headwayMin ?? "?"}–
                  {activeService?.headwayMax ?? "?"} phút ·{" "}
                  <span className="font-semibold">Lịch chạy:</span>{" "}
                  {activeService?.serviceDays ?? "Unknown"}
                </p>
                {routeDetail.fares.length > 0 && (
                  <p>
                    <span className="font-semibold">Giá vé:</span>{" "}
                    {routeDetail.fares
                      .map((f) =>
                        f.referenceAmount == null
                          ? "Chưa có"
                          : `${f.referenceAmount.toLocaleString(
                              "vi-VN"
                            )} ${f.currency} (${f.description || "Vé lượt"})`
                      )
                      .join("; ")}
                  </p>
                )}
              </div>

              {/* Schematic Route Map */}
              <TransitRouteMap stops={activeStops} />

              {/* Stop order table */}
              <div className="max-h-60 overflow-y-auto rounded-md border">
                <Table>
                  <TableHeader className="bg-muted/50">
                    <TableRow>
                      <TableHead className="w-12 text-center">#</TableHead>
                      <TableHead className="w-[120px]">Mã trạm</TableHead>
                      <TableHead>Tên trạm</TableHead>
                      <TableHead>Địa chỉ</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {activeStops.map((s, idx) => (
                      <TableRow key={`${s.id}-${idx}`}>
                        <TableCell className="text-center font-medium text-xs">
                          {s.sequence ?? idx + 1}
                        </TableCell>
                        <TableCell className="font-mono text-xs">
                          {s.externalId}
                        </TableCell>
                        <TableCell className="font-medium text-xs">
                          {s.name}
                        </TableCell>
                        <TableCell className="text-xs text-muted-foreground">
                          {s.address || "—"}
                        </TableCell>
                      </TableRow>
                    ))}
                  </TableBody>
                </Table>
              </div>
            </div>
          )}
        </DialogContent>
      </Dialog>
    </div>
  );
}
