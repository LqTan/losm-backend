"use client";

import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import { api, ApiError } from "@/lib/api";
import { getToken } from "@/lib/auth";
import type { PagedResult, Place } from "@/lib/types";
import {
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  ExternalLink,
  Filter,
  Loader2,
  MapPin,
  Navigation,
  Pencil,
  RefreshCw,
  Search,
  Trash2,
  X,
} from "lucide-react";
import Link from "next/link";
import { useCallback, useEffect, useState } from "react";
import { toast } from "sonner";

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

export default function PlacesAdminPage() {
  const [places, setPlaces] = useState<Place[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(15);
  const [totalPages, setTotalPages] = useState(1);
  const [loading, setLoading] = useState(true);

  // Filters
  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");
  const [sourceFilter, setSourceFilter] = useState("");
  const [categoryFilter, setCategoryFilter] = useState("");

  // Filter options
  const [sources, setSources] = useState<string[]>([]);
  const [categories, setCategories] = useState<string[]>([]);

  // Dialogs
  const [editing, setEditing] = useState<Place | null>(null);
  const [deleting, setDeleting] = useState<Place | null>(null);
  const [deletingLoading, setDeletingLoading] = useState(false);

  const loadFilterOptions = useCallback(async () => {
    try {
      const token = getToken();
      const [srcRes, catRes] = await Promise.allSettled([
        api.get<string[]>("/api/places/sources", token),
        api.get<string[]>("/api/places/categories", token),
      ]);
      if (srcRes.status === "fulfilled") {
        setSources(srcRes.value.filter(Boolean));
      }
      if (catRes.status === "fulfilled") {
        setCategories(catRes.value.filter(Boolean));
      }
    } catch {
      // ignore
    }
  }, []);

  const loadPlaces = useCallback(
    async (
      targetPage: number = page,
      targetSearch: string = search,
      targetSource: string = sourceFilter,
      targetCategory: string = categoryFilter,
      targetPageSize: number = pageSize,
    ) => {
      setLoading(true);
      try {
        const token = getToken();
        const params = new URLSearchParams({
          page: targetPage.toString(),
          pageSize: targetPageSize.toString(),
        });
        if (targetSearch.trim()) params.set("search", targetSearch.trim());
        if (targetSource.trim()) params.set("source", targetSource.trim());
        if (targetCategory.trim()) params.set("category", targetCategory.trim());

        const res = await api.get<PagedResult<Place>>(
          `/api/places?${params.toString()}`,
          token,
        );

        setPlaces(res.items);
        setTotalCount(res.totalCount);
        setPage(res.page);
        setTotalPages(Math.max(1, res.totalPages));
      } catch (err) {
        toast.error(
          err instanceof ApiError ? err.message : "Không thể tải danh sách địa điểm",
        );
      } finally {
        setLoading(false);
      }
    },
    [page, search, sourceFilter, categoryFilter, pageSize],
  );

  useEffect(() => {
    void loadFilterOptions();
  }, [loadFilterOptions]);

  useEffect(() => {
    void loadPlaces(page, search, sourceFilter, categoryFilter, pageSize);
  }, [page, search, sourceFilter, categoryFilter, pageSize, loadPlaces]);

  function handleSearchSubmit(e: React.FormEvent) {
    e.preventDefault();
    setPage(1);
    setSearch(searchInput);
  }

  function handleResetFilters() {
    setSearchInput("");
    setSearch("");
    setSourceFilter("");
    setCategoryFilter("");
    setPage(1);
  }

  async function handleDeleteConfirm() {
    if (!deleting) return;
    setDeletingLoading(true);
    try {
      const token = getToken();
      await api.del(`/api/places/${deleting.id}`, token);
      toast.success(`Đã xóa địa điểm "${deleting.name}"`);
      setDeleting(null);
      void loadPlaces();
      void loadFilterOptions();
    } catch (err) {
      toast.error(
        err instanceof ApiError ? err.message : "Xóa địa điểm thất bại",
      );
    } finally {
      setDeletingLoading(false);
    }
  }

  const hasActiveFilters = Boolean(
    search.trim() || sourceFilter.trim() || categoryFilter.trim(),
  );

  return (
    <div className="space-y-6">
      {/* Top Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-2xl font-bold tracking-tight">Quản lý Địa điểm</h2>
          <p className="text-sm text-muted-foreground">
            Xem, tìm kiếm, sửa và xóa địa điểm được nạp từ Overture Maps.
          </p>
        </div>
        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => {
              void loadPlaces();
              void loadFilterOptions();
            }}
            disabled={loading}
          >
            <RefreshCw
              className={`mr-1.5 h-3.5 w-3.5 ${loading ? "animate-spin" : ""}`}
            />
            Làm mới
          </Button>
        </div>
      </div>

      {/* Filter and Search Bar */}
      <Card>
        <CardContent className="pt-5">
          <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
            {/* Search Input */}
            <form
              onSubmit={handleSearchSubmit}
              className="flex flex-1 items-center gap-2"
            >
              <div className="relative flex-1">
                <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
                <Input
                  type="text"
                  placeholder="Tìm theo tên hoặc địa chỉ..."
                  value={searchInput}
                  onChange={(e) => setSearchInput(e.target.value)}
                  className="pl-9 text-sm"
                />
              </div>
              <Button type="submit" size="sm" variant="secondary">
                Tìm
              </Button>
            </form>

            {/* Dropdown Filters */}
            <div className="flex flex-wrap items-center gap-2">
              {/* Source Filter */}
              <select
                value={sourceFilter}
                onChange={(e) => {
                  setSourceFilter(e.target.value);
                  setPage(1);
                }}
                className="h-9 rounded-md border border-input bg-background px-3 py-1 text-xs shadow-xs focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="">Tất cả nguồn (Source)</option>
                {sources.map((s) => (
                  <option key={s} value={s}>
                    {s}
                  </option>
                ))}
              </select>

              {/* Category Filter */}
              <select
                value={categoryFilter}
                onChange={(e) => {
                  setCategoryFilter(e.target.value);
                  setPage(1);
                }}
                className="h-9 max-w-[200px] rounded-md border border-input bg-background px-3 py-1 text-xs shadow-xs focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="">Tất cả danh mục</option>
                {categories.map((c) => (
                  <option key={c} value={c}>
                    {c}
                  </option>
                ))}
              </select>

              {/* Page size select */}
              <select
                value={pageSize}
                onChange={(e) => {
                  setPageSize(Number(e.target.value));
                  setPage(1);
                }}
                className="h-9 rounded-md border border-input bg-background px-2.5 py-1 text-xs shadow-xs focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value={10}>10 / trang</option>
                <option value={15}>15 / trang</option>
                <option value={25}>25 / trang</option>
                <option value={50}>50 / trang</option>
              </select>

              {hasActiveFilters && (
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={handleResetFilters}
                  className="h-9 px-2.5 text-xs text-muted-foreground hover:text-foreground"
                >
                  <X className="mr-1 h-3.5 w-3.5" />
                  Đặt lại
                </Button>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Places Table */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
          <div>
            <CardTitle className="text-base font-semibold">
              Danh sách địa điểm
            </CardTitle>
            <CardDescription className="text-xs">
              Tổng cộng {totalCount.toLocaleString()} địa điểm
              {hasActiveFilters ? " (đang lọc)" : ""}
            </CardDescription>
          </div>
        </CardHeader>
        <CardContent className="p-0">
          <div className="relative overflow-x-auto max-h-[calc(100vh-320px)] min-h-[360px] overflow-y-auto">
            <Table>
              <TableHeader className="sticky top-0 z-10 bg-muted/90 backdrop-blur shadow-xs">
                <TableRow>
                  <TableHead className="w-[280px]">Tên địa điểm</TableHead>
                  <TableHead className="w-[140px]">Danh mục</TableHead>
                  <TableHead>Địa chỉ</TableHead>
                  <TableHead className="w-[170px]">Tọa độ (Lat, Lng)</TableHead>
                  <TableHead className="w-[110px]">Nguồn</TableHead>
                  <TableHead className="w-[120px] text-right">Thao tác</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loading ? (
                  <TableRow>
                    <TableCell
                      colSpan={6}
                      className="h-32 text-center text-sm text-muted-foreground"
                    >
                      <div className="flex items-center justify-center gap-2">
                        <Loader2 className="h-4 w-4 animate-spin text-primary" />
                        <span>Đang tải dữ liệu...</span>
                      </div>
                    </TableCell>
                  </TableRow>
                ) : places.length === 0 ? (
                  <TableRow>
                    <TableCell
                      colSpan={6}
                      className="h-32 text-center text-sm text-muted-foreground"
                    >
                      <div className="flex flex-col items-center justify-center gap-1.5">
                        <MapPin className="h-6 w-6 text-muted-foreground/50" />
                        <span>Không tìm thấy địa điểm nào phù hợp.</span>
                        {hasActiveFilters && (
                          <Button
                            variant="link"
                            size="sm"
                            onClick={handleResetFilters}
                            className="h-auto p-0 text-xs"
                          >
                            Xóa bộ lọc để xem tất cả
                          </Button>
                        )}
                      </div>
                    </TableCell>
                  </TableRow>
                ) : (
                  places.map((place) => (
                    <TableRow key={place.id} className="hover:bg-muted/40">
                      <TableCell>
                        <div className="flex flex-col gap-0.5">
                          <span className="font-medium text-foreground">
                            {place.name}
                          </span>
                          <span
                            className="font-mono text-[10px] text-muted-foreground truncate max-w-[240px]"
                            title={place.externalId}
                          >
                            ID: {place.externalId}
                          </span>
                        </div>
                      </TableCell>
                      <TableCell>
                        {place.category ? (
                          <Badge
                            variant="secondary"
                            className="text-[11px] font-normal"
                          >
                            {place.category}
                          </Badge>
                        ) : (
                          <span className="text-xs text-muted-foreground">—</span>
                        )}
                      </TableCell>
                      <TableCell>
                        <span
                          className="text-xs text-muted-foreground line-clamp-2 max-w-[320px]"
                          title={place.address ?? ""}
                        >
                          {place.address || "—"}
                        </span>
                      </TableCell>
                      <TableCell>
                        <div className="flex items-center gap-1.5 font-mono text-[11px] text-muted-foreground">
                          <span>
                            {place.latitude.toFixed(4)}, {place.longitude.toFixed(4)}
                          </span>
                          <a
                            href={`https://www.google.com/maps?q=${place.latitude},${place.longitude}`}
                            target="_blank"
                            rel="noreferrer"
                            title="Xem trên Google Maps"
                            className="text-muted-foreground/70 hover:text-primary transition-colors"
                          >
                            <ExternalLink className="h-3 w-3" />
                          </a>
                        </div>
                      </TableCell>
                      <TableCell>
                        <Badge
                          variant="outline"
                          className="text-[11px] font-normal uppercase tracking-wider"
                        >
                          {place.source}
                        </Badge>
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex items-center justify-end gap-1">
                          <Button
                            variant="ghost"
                            size="icon"
                            className="h-8 w-8 text-muted-foreground hover:text-foreground"
                            title="Chỉnh sửa"
                            onClick={() => setEditing(place)}
                          >
                            <Pencil className="h-3.5 w-3.5" />
                          </Button>
                          <Button
                            variant="ghost"
                            size="icon"
                            className="h-8 w-8 text-muted-foreground hover:text-destructive"
                            title="Xóa"
                            onClick={() => setDeleting(place)}
                          >
                            <Trash2 className="h-3.5 w-3.5" />
                          </Button>
                        </div>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </div>

          {/* Pagination Controls - Always visible */}
          <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between border-t px-4 py-3 bg-card">
            <div className="flex flex-wrap items-center gap-4 text-xs text-muted-foreground">
              <div>
                Hiển thị{" "}
                <span className="font-semibold text-foreground">
                  {totalCount === 0 ? 0 : (page - 1) * pageSize + 1}
                </span>{" "}
                –{" "}
                <span className="font-semibold text-foreground">
                  {Math.min(page * pageSize, totalCount)}
                </span>{" "}
                trong{" "}
                <span className="font-semibold text-foreground">
                  {totalCount.toLocaleString()}
                </span>{" "}
                địa điểm
              </div>

              {/* Page size dropdown */}
              <div className="flex items-center gap-1.5">
                <span>Số dòng:</span>
                <select
                  value={pageSize}
                  onChange={(e) => {
                    const newSize = Number(e.target.value);
                    setPageSize(newSize);
                    setPage(1);
                  }}
                  className="h-8 rounded-md border border-input bg-background px-2 py-1 text-xs shadow-xs focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring text-foreground font-medium cursor-pointer"
                >
                  <option value={10}>10 / trang</option>
                  <option value={15}>15 / trang</option>
                  <option value={20}>20 / trang</option>
                  <option value={50}>50 / trang</option>
                  <option value={100}>100 / trang</option>
                </select>
              </div>
            </div>

            {/* Navigation buttons */}
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

              {/* Page Number Pills */}
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
                ),
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

      {/* Place Edit Modal */}
      {editing && (
        <PlaceEditorModal
          place={editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            void loadPlaces();
            void loadFilterOptions();
          }}
        />
      )}

      {/* Delete Confirmation Modal */}
      {deleting && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs">
          <Card className="w-full max-w-md rounded-xl border bg-card text-card-foreground shadow-xl">
            <CardHeader className="border-b px-5 py-4">
              <CardTitle className="text-base font-semibold text-destructive">
                Xác nhận xóa địa điểm
              </CardTitle>
              <CardDescription className="text-xs">
                Hành động này sẽ xóa vĩnh viễn địa điểm khỏi cơ sở dữ liệu.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 p-5">
              <p className="text-sm">
                Bạn có chắc chắn muốn xóa địa điểm{" "}
                <span className="font-semibold text-foreground">
                  &quot;{deleting.name}&quot;
                </span>{" "}
                không?
              </p>
              <div className="rounded-md border bg-muted/40 p-3 text-xs space-y-1">
                <div>
                  <span className="text-muted-foreground">Nguồn: </span>
                  <span className="font-mono">{deleting.source}</span>
                </div>
                {deleting.address && (
                  <div>
                    <span className="text-muted-foreground">Địa chỉ: </span>
                    <span>{deleting.address}</span>
                  </div>
                )}
                <div>
                  <span className="text-muted-foreground">Tọa độ: </span>
                  <span className="font-mono">
                    {deleting.latitude.toFixed(6)}, {deleting.longitude.toFixed(6)}
                  </span>
                </div>
              </div>
              <div className="flex justify-end gap-2 pt-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setDeleting(null)}
                  disabled={deletingLoading}
                >
                  Hủy
                </Button>
                <Button
                  variant="destructive"
                  size="sm"
                  onClick={handleDeleteConfirm}
                  disabled={deletingLoading}
                >
                  {deletingLoading && (
                    <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
                  )}
                  Xác nhận xóa
                </Button>
              </div>
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  );
}

function PlaceEditorModal({
  place,
  onClose,
  onSaved,
}: {
  place: Place;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [name, setName] = useState(place.name);
  const [category, setCategory] = useState(place.category ?? "");
  const [address, setAddress] = useState(place.address ?? "");
  const [latitude, setLatitude] = useState<string>(
    place.latitude !== undefined ? place.latitude.toString() : "",
  );
  const [longitude, setLongitude] = useState<string>(
    place.longitude !== undefined ? place.longitude.toString() : "",
  );
  const [source, setSource] = useState(place.source ?? "Overture");
  const [openingHours, setOpeningHours] = useState(place.openingHours ?? "");
  const [saving, setSaving] = useState(false);

  function handleGetCurrentLocation() {
    if (!navigator.geolocation) {
      toast.error("Trình duyệt không hỗ trợ Geolocation");
      return;
    }
    toast.info("Đang lấy tọa độ hiện tại...");
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setLatitude(pos.coords.latitude.toFixed(6));
        setLongitude(pos.coords.longitude.toFixed(6));
        toast.success("Đã cập nhật tọa độ hiện tại!");
      },
      (err) => {
        toast.error(`Không thể lấy tọa độ: ${err.message}`);
      },
      { enableHighAccuracy: true, timeout: 10000 },
    );
  }

  async function handleSave(e: React.FormEvent) {
    e.preventDefault();
    const trimmedName = name.trim();
    if (!trimmedName) {
      toast.error("Tên địa điểm không được để trống.");
      return;
    }

    const lat = parseFloat(latitude);
    const lng = parseFloat(longitude);

    if (isNaN(lat) || lat < -90 || lat > 90) {
      toast.error("Vĩ độ (Latitude) phải là số từ -90 đến 90.");
      return;
    }

    if (isNaN(lng) || lng < -180 || lng > 180) {
      toast.error("Kinh độ (Longitude) phải là số từ -180 đến 180.");
      return;
    }

    setSaving(true);
    try {
      const token = getToken();
      await api.put(
        `/api/places/${place.id}`,
        {
          id: place.id,
          name: trimmedName,
          address: address.trim() || null,
          category: category.trim() || null,
          latitude: lat,
          longitude: lng,
          openingHours: openingHours.trim() || null,
          source: source.trim() || place.source || "Overture",
        },
        token,
      );
      toast.success("Cập nhật địa điểm thành công!");
      onSaved();
    } catch (err) {
      toast.error(
        err instanceof ApiError ? err.message : "Lưu địa điểm thất bại",
      );
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs">
      <Card className="flex max-h-[92vh] w-full max-w-xl flex-col overflow-hidden rounded-xl border bg-card text-card-foreground shadow-xl">
        <CardHeader className="border-b px-5 py-4">
          <div className="flex items-center justify-between">
            <div>
              <CardTitle className="text-base font-semibold">
                Chỉnh sửa địa điểm
              </CardTitle>
              <CardDescription className="text-xs">
                Cập nhật thông tin cho &quot;{place.name}&quot;
              </CardDescription>
            </div>
            <Button
              variant="ghost"
              size="icon"
              className="h-8 w-8 text-muted-foreground hover:text-foreground"
              onClick={onClose}
            >
              <X className="h-4 w-4" />
            </Button>
          </div>
        </CardHeader>

        <form onSubmit={handleSave} className="flex flex-col flex-1 overflow-hidden">
          <CardContent className="flex-1 overflow-y-auto space-y-4 p-5">
            {/* Tên địa điểm */}
            <div className="space-y-1.5">
              <Label className="text-xs font-medium">
                Tên địa điểm <span className="text-destructive">*</span>
              </Label>
              <Input
                value={name}
                onChange={(e) => setName(e.target.value)}
                placeholder="Ví dụ: Highland Coffee, Chợ Bến Thành..."
                className="h-9 text-sm"
                required
              />
            </div>

            {/* Danh mục & Nguồn */}
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
              <div className="space-y-1.5">
                <Label className="text-xs font-medium">Danh mục (Category)</Label>
                <Input
                  value={category}
                  onChange={(e) => setCategory(e.target.value)}
                  placeholder="cafe, restaurant, shop..."
                  className="h-9 text-sm"
                />
              </div>
              <div className="space-y-1.5">
                <Label className="text-xs font-medium">Nguồn dữ liệu (Source)</Label>
                <Input
                  value={source}
                  onChange={(e) => setSource(e.target.value)}
                  placeholder="Overture, Manual, HERE..."
                  className="h-9 text-sm"
                />
              </div>
            </div>

            {/* Địa chỉ */}
            <div className="space-y-1.5">
              <Label className="text-xs font-medium">Địa chỉ chi tiết</Label>
              <Input
                value={address}
                onChange={(e) => setAddress(e.target.value)}
                placeholder="Số nhà, đường, phường, quận..."
                className="h-9 text-sm"
              />
            </div>

            {/* Tọa độ (Vĩ độ & Kinh độ) */}
            <div className="space-y-2 rounded-lg border bg-muted/30 p-3">
              <div className="flex items-center justify-between">
                <Label className="text-xs font-semibold">
                  Tọa độ địa lý <span className="text-destructive">*</span>
                </Label>
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={handleGetCurrentLocation}
                  className="h-7 text-[11px] px-2 gap-1"
                >
                  <Navigation className="h-3 w-3" />
                  Vị trí hiện tại
                </Button>
              </div>

              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1">
                  <Label className="text-[11px] text-muted-foreground">
                    Vĩ độ (Latitude: -90 đến 90)
                  </Label>
                  <Input
                    type="number"
                    step="any"
                    value={latitude}
                    onChange={(e) => setLatitude(e.target.value)}
                    placeholder="Ví dụ: 10.7769"
                    className="h-9 text-sm font-mono"
                    required
                  />
                </div>
                <div className="space-y-1">
                  <Label className="text-[11px] text-muted-foreground">
                    Kinh độ (Longitude: -180 đến 180)
                  </Label>
                  <Input
                    type="number"
                    step="any"
                    value={longitude}
                    onChange={(e) => setLongitude(e.target.value)}
                    placeholder="Ví dụ: 106.7009"
                    className="h-9 text-sm font-mono"
                    required
                  />
                </div>
              </div>
            </div>

            {/* Giờ mở cửa */}
            <div className="space-y-1.5">
              <Label className="text-xs font-medium">Giờ mở cửa (Opening Hours)</Label>
              <Input
                value={openingHours}
                onChange={(e) => setOpeningHours(e.target.value)}
                placeholder="Mo-Su 07:00-22:00..."
                className="h-9 text-sm"
              />
            </div>
          </CardContent>

          <div className="flex items-center justify-end gap-2 border-t bg-muted/20 px-5 py-3">
            <Button
              type="button"
              variant="outline"
              size="sm"
              onClick={onClose}
              disabled={saving}
            >
              Hủy
            </Button>
            <Button type="submit" size="sm" disabled={saving}>
              {saving && <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />}
              Lưu thay đổi
            </Button>
          </div>
        </form>
      </Card>
    </div>
  );
}
