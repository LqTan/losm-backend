"use client";

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
import BboxPickerMap from "@/components/admin/BboxPickerMap";
import { api, ApiError } from "@/lib/api";
import { getToken } from "@/lib/auth";
import type { Area } from "@/lib/types";
import {
  Download,
  Loader2,
  MapPin,
  Pencil,
  Plus,
  Search,
  Trash2,
  Upload,
  X,
} from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { toast } from "sonner";

export default function AreasAdminPage() {
  const [areas, setAreas] = useState<Area[]>([]);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<Area | null>(null);
  const [creating, setCreating] = useState(false);

  async function load() {
    setLoading(true);
    try {
      const data = await api.get<Area[]>("/api/areas", getToken());
      setAreas(data);
    } catch (err) {
      toast.error(
        err instanceof ApiError ? err.message : "Failed to load areas",
      );
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    void load();
  }, []);

  async function onDelete(id: string) {
    if (!confirm("Bạn có chắc chắn muốn xóa khu vực này?")) return;
    try {
      await api.del(`/api/areas/${id}`, getToken());
      toast.success("Đã xóa khu vực");
      await load();
    } catch (err) {
      toast.error(
        err instanceof ApiError ? err.message : "Xóa khu vực thất bại",
      );
    }
  }

  const fileInputRef = useRef<HTMLInputElement | null>(null);
  const [importing, setImporting] = useState(false);

  async function onFileSelected(e: React.ChangeEvent<HTMLInputElement>) {
    const file = e.target.files?.[0];
    if (!file) return;
    setImporting(true);
    try {
      const form = new FormData();
      form.append("file", file);
      const token = getToken();
      const base = (await import("@/lib/api")).getApiBase();
      const res = await fetch(`${base}/api/areas/import`, {
        method: "POST",
        body: form,
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      });
      const data = await res.json().catch(() => null);
      if (!res.ok) {
        throw new Error(data?.message ?? `HTTP ${res.status}`);
      }
      const result = data as {
        total: number;
        inserted: number;
        updated: number;
        skipped: number;
        errors?: string[];
      };
      toast.success(
        `Imported: ${result.inserted} new, ${result.updated} updated, ${result.skipped} skipped`,
      );
      if (result.errors && result.errors.length > 0) {
        toast.warning(`${result.errors.length} cảnh báo (xem console)`);
        console.warn("Import errors:", result.errors);
      }
      await load();
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Import thất bại");
    } finally {
      setImporting(false);
      if (fileInputRef.current) fileInputRef.current.value = "";
    }
  }

  async function downloadTemplate() {
    try {
      const token = getToken();
      const base = (await import("@/lib/api")).getApiBase();
      const res = await fetch(`${base}/api/areas/template`, {
        headers: token ? { Authorization: `Bearer ${token}` } : {},
      });
      if (!res.ok) throw new Error(`HTTP ${res.status}`);
      const blob = await res.blob();
      const url = URL.createObjectURL(blob);
      const a = document.createElement("a");
      a.href = url;
      a.download = "vn-areas.json";
      document.body.appendChild(a);
      a.click();
      a.remove();
      URL.revokeObjectURL(url);
    } catch (err) {
      toast.error(err instanceof Error ? err.message : "Download thất bại");
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-2xl font-bold tracking-tight">Khu vực (Areas)</h2>
          <p className="text-sm text-muted-foreground">
            Quản lý các khu vực, quận/huyện để tải và quét dữ liệu địa điểm.
          </p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <Button variant="outline" size="sm" onClick={downloadTemplate}>
            <Download className="mr-1.5 h-3.5 w-3.5" />
            Template
          </Button>
          <Button
            variant="outline"
            size="sm"
            onClick={() => fileInputRef.current?.click()}
            disabled={importing}
          >
            {importing ? (
              <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
            ) : (
              <Upload className="mr-1.5 h-3.5 w-3.5" />
            )}
            Import JSON
          </Button>
          <input
            ref={fileInputRef}
            type="file"
            accept=".json,application/json"
            className="hidden"
            onChange={onFileSelected}
          />
          <Button size="sm" onClick={() => setCreating(true)} className="gap-1.5">
            <Plus className="h-4 w-4" />
            Thêm khu vực
          </Button>
        </div>
      </div>

      <Card>
        <CardHeader className="pb-3">
          <CardTitle className="text-base font-semibold">
            Danh sách khu vực ({areas.length})
          </CardTitle>
          <CardDescription className="text-xs">
            Các quận/huyện và vùng địa lý đã thiết lập bounding box
          </CardDescription>
        </CardHeader>
        <CardContent className="p-0">
          {loading ? (
            <div className="flex h-32 items-center justify-center gap-2 text-sm text-muted-foreground">
              <Loader2 className="h-4 w-4 animate-spin text-primary" />
              <span>Đang tải danh sách khu vực...</span>
            </div>
          ) : areas.length === 0 ? (
            <div className="flex h-32 flex-col items-center justify-center gap-1 text-sm text-muted-foreground">
              <MapPin className="h-6 w-6 text-muted-foreground/50" />
              <span>Chưa có khu vực nào được lưu.</span>
            </div>
          ) : (
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead className="w-[300px]">Tên khu vực (Quận)</TableHead>
                  <TableHead>Tọa độ Bounding Box (Bbox)</TableHead>
                  <TableHead className="w-24 text-right">Thao tác</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {areas.map((a) => (
                  <TableRow key={a.id} className="hover:bg-muted/40">
                    <TableCell className="font-medium text-foreground">
                      {a.name}
                    </TableCell>
                    <TableCell className="font-mono text-xs text-muted-foreground">
                      {a.bboxMinLng.toFixed(4)}, {a.bboxMinLat.toFixed(4)} →{" "}
                      {a.bboxMaxLng.toFixed(4)}, {a.bboxMaxLat.toFixed(4)}
                    </TableCell>
                    <TableCell className="text-right">
                      <div className="flex items-center justify-end gap-1">
                        <Button
                          size="icon"
                          variant="ghost"
                          className="h-8 w-8 text-muted-foreground hover:text-foreground"
                          title="Chỉnh sửa"
                          onClick={() => setEditing(a)}
                        >
                          <Pencil className="h-3.5 w-3.5" />
                        </Button>
                        <Button
                          size="icon"
                          variant="ghost"
                          className="h-8 w-8 text-muted-foreground hover:text-destructive"
                          title="Xóa"
                          onClick={() => onDelete(a.id)}
                        >
                          <Trash2 className="h-3.5 w-3.5" />
                        </Button>
                      </div>
                    </TableCell>
                  </TableRow>
                ))}
              </TableBody>
            </Table>
          )}
        </CardContent>
      </Card>

      {(editing || creating) && (
        <AreaEditorDialog
          area={editing}
          onClose={() => {
            setEditing(null);
            setCreating(false);
          }}
          onSaved={() => {
            setEditing(null);
            setCreating(false);
            void load();
          }}
        />
      )}
    </div>
  );
}

function AreaEditorDialog({
  area,
  onClose,
  onSaved,
}: {
  area: Area | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [name, setName] = useState(area?.name ?? "");
  const [minLat, setMinLat] = useState(area?.bboxMinLat ?? 10.7535);
  const [minLng, setMinLng] = useState(area?.bboxMinLng ?? 106.6816);
  const [maxLat, setMaxLat] = useState(area?.bboxMaxLat ?? 10.7969);
  const [maxLng, setMaxLng] = useState(area?.bboxMaxLng ?? 106.7151);

  const [searching, setSearching] = useState(false);
  const [foundAddress, setFoundAddress] = useState<string | null>(null);
  // eslint-disable-next-line @typescript-eslint/no-explicit-any
  const [geoJson, setGeoJson] = useState<any | null>(null);
  const [saving, setSaving] = useState(false);

  async function onSearchNominatim() {
    const query = name.trim();
    if (!query) {
      toast.error("Vui lòng nhập tên quận/khu vực cần tìm (ví dụ: Quận 1)");
      return;
    }

    setSearching(true);
    setFoundAddress(null);
    setGeoJson(null);
    try {
      // 1. Try our backend lookup-bbox first
      let resolved = false;
      try {
        const token = getToken();
        const res = await api.get<{
          name: string;
          displayName?: string;
          minLat: number;
          minLng: number;
          maxLat: number;
          maxLng: number;
          geojson?: unknown;
        }>(`/api/areas/lookup-bbox?query=${encodeURIComponent(query)}`, token);

        if (res && res.minLat && res.maxLat) {
          setMinLat(res.minLat);
          setMinLng(res.minLng);
          setMaxLat(res.maxLat);
          setMaxLng(res.maxLng);
          setFoundAddress(res.displayName ?? null);
          if (res.geojson) {
            setGeoJson(res.geojson);
          }
          toast.success(`Đã tìm thấy "${res.name}"!`);
          resolved = true;
        }
      } catch {
        // Fallback to direct Nominatim query
      }

      // 2. Direct Nominatim fallback if backend not reached or not found
      if (!resolved) {
        const osmUrl = `https://nominatim.openstreetmap.org/search?q=${encodeURIComponent(
          query.includes("Việt Nam") || query.includes("Hồ Chí Minh")
            ? query
            : `${query}, Hồ Chí Minh, Việt Nam`,
        )}&format=jsonv2&limit=1&addressdetails=1&polygon_geojson=1`;

        const response = await fetch(osmUrl, {
          headers: {
            Accept: "application/json",
          },
        });

        if (response.ok) {
          const items = (await response.json()) as Array<{
            name?: string;
            display_name?: string;
            boundingbox?: string[];
            geojson?: unknown;
          }>;

          if (items && items.length > 0 && items[0].boundingbox?.length === 4) {
            const bbox = items[0].boundingbox;
            const osmMinLat = parseFloat(bbox[0]);
            const osmMaxLat = parseFloat(bbox[1]);
            const osmMinLng = parseFloat(bbox[2]);
            const osmMaxLng = parseFloat(bbox[3]);

            setMinLat(osmMinLat);
            setMaxLat(osmMaxLat);
            setMinLng(osmMinLng);
            setMaxLng(osmMaxLng);
            setFoundAddress(items[0].display_name ?? null);
            if (items[0].geojson) {
              setGeoJson(items[0].geojson);
            }
            toast.success(`Đã tìm thấy "${items[0].name || query}"!`);
            resolved = true;
          }
        }
      }

      if (!resolved) {
        toast.error(`Không tìm thấy Bbox cho "${query}". Vui lòng kiểm tra lại tên quận/khu vực.`);
      }
    } catch (err) {
      toast.error(
        err instanceof Error ? err.message : "Tìm kiếm vị trí thất bại",
      );
    } finally {
      setSearching(false);
    }
  }

  async function onSave() {
    if (!name.trim()) {
      toast.error("Vui lòng nhập tên khu vực");
      return;
    }
    setSaving(true);
    try {
      const payload = {
        name: name.trim(),
        level: 2, // Quận / District
        parentId: null,
        externalCode: null,
        bboxMinLat: minLat,
        bboxMinLng: minLng,
        bboxMaxLat: maxLat,
        bboxMaxLng: maxLng,
        isActive: true,
      };

      if (area) {
        await api.put(`/api/areas/${area.id}`, { ...payload, id: area.id }, getToken());
      } else {
        await api.post("/api/areas", payload, getToken());
      }
      toast.success(area ? "Cập nhật khu vực thành công!" : "Tạo mới khu vực thành công!");
      onSaved();
    } catch (err) {
      toast.error(err instanceof ApiError ? err.message : "Lưu khu vực thất bại");
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs">
      <Card className="flex max-h-[92vh] w-full max-w-2xl flex-col overflow-hidden rounded-xl border bg-card text-card-foreground shadow-xl">
        <CardHeader className="border-b px-5 py-3.5">
          <div className="flex items-center justify-between">
            <div>
              <CardTitle className="text-base font-semibold">
                {area ? "Chỉnh sửa khu vực" : "Thêm khu vực (Quận)"}
              </CardTitle>
              <CardDescription className="text-xs">
                Nhập tên quận để tự động tìm ranh giới chính xác và xem Bbox từ OpenStreetMap.
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

        <CardContent className="flex-1 space-y-4 overflow-y-auto px-5 py-4">
          {/* Ô nhập tên duy nhất + Nút Tìm */}
          <div className="space-y-1.5">
            <Label className="text-xs font-medium">
              Tên quận / khu vực <span className="text-destructive">*</span>
            </Label>
            <div className="flex gap-2">
              <Input
                value={name}
                onChange={(e) => setName(e.target.value)}
                onKeyDown={(e) => {
                  if (e.key === "Enter") {
                    e.preventDefault();
                    void onSearchNominatim();
                  }
                }}
                placeholder="Ví dụ: Quận 1, Quận Bình Thạnh, Quận 7..."
                className="h-9 text-sm"
                autoFocus
              />
              <Button
                type="button"
                onClick={onSearchNominatim}
                disabled={searching}
                size="sm"
                className="h-9 shrink-0 gap-1.5 px-3.5"
              >
                {searching ? (
                  <Loader2 className="h-3.5 w-3.5 animate-spin" />
                ) : (
                  <Search className="h-3.5 w-3.5" />
                )}
                Tìm vị trí
              </Button>
            </div>
            {foundAddress && (
              <p className="text-[11px] text-muted-foreground">
                <span className="font-medium text-foreground">Vị trí: </span>
                {foundAddress}
              </p>
            )}
          </div>

          {/* Bản đồ Bbox & Ranh giới GeoJSON */}
          <div className="space-y-1.5">
            <div className="flex items-center justify-between">
              <Label className="text-xs font-medium">Bản đồ Bbox & Ranh giới</Label>
              {Boolean(geoJson) && (
                <span className="text-[11px] text-primary font-medium">
                  ✓ Đã tải ranh giới chính xác (GeoJSON)
                </span>
              )}
            </div>
            <BboxPickerMap
              value={{
                minLat,
                minLng,
                maxLat,
                maxLng,
              }}
              geoJson={geoJson}
            />
            <div className="flex flex-wrap items-center gap-3 font-mono text-[11px] text-muted-foreground pt-1">
              <span>minLat: {minLat.toFixed(4)}</span>
              <span>minLng: {minLng.toFixed(4)}</span>
              <span>maxLat: {maxLat.toFixed(4)}</span>
              <span>maxLng: {maxLng.toFixed(4)}</span>
            </div>
          </div>
        </CardContent>

        <div className="flex justify-end gap-2 border-t bg-muted/20 px-5 py-3">
          <Button variant="outline" size="sm" onClick={onClose} disabled={saving}>
            Hủy
          </Button>
          <Button size="sm" onClick={onSave} disabled={saving}>
            {saving && <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />}
            Lưu
          </Button>
        </div>
      </Card>
    </div>
  );
}