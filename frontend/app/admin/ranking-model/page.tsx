"use client";

import { useEffect, useState, useRef } from "react";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { api, ApiError, getApiBase } from "@/lib/api";
import { getToken } from "@/lib/auth";
import {
  Cpu,
  Upload,
  CheckCircle2,
  AlertCircle,
  Trash2,
  Power,
  Download,
  FileCode,
  FileText,
  Info,
  Loader2,
  Sparkles,
  RefreshCw,
} from "lucide-react";
import { toast } from "sonner";

interface ModelStatus {
  hasModel: boolean;
  isActive: boolean;
  modelName: string | null;
  fileSizeBytes: number;
  uploadedAt: string | null;
  inputName: string | null;
  featureCount: number | null;
  statusMessage: string | null;
}

export default function RankingModelPage() {
  const [status, setStatus] = useState<ModelStatus | null>(null);
  const [loading, setLoading] = useState(true);
  const [uploading, setUploading] = useState(false);
  const [toggling, setToggling] = useState(false);
  const [selectedFile, setSelectedFile] = useState<File | null>(null);
  const [dragOver, setDragOver] = useState(false);

  const fileInputRef = useRef<HTMLInputElement>(null);

  async function fetchStatus() {
    setLoading(true);
    try {
      const token = getToken();
      const res = await api.get<ModelStatus>("/api/admin/ranking-model/status", token);
      setStatus(res);
    } catch (err) {
      console.error("Error fetching model status:", err);
      toast.error("Không thể lấy thông tin mô hình AI.");
    } finally {
      setLoading(false);
    }
  }

  useEffect(() => {
    fetchStatus();
  }, []);

  async function handleUpload() {
    if (!selectedFile) {
      toast.error("Vui lòng chọn một file .onnx");
      return;
    }

    if (!selectedFile.name.toLowerCase().endsWith(".onnx")) {
      toast.error("Chỉ chấp nhận file định dạng .onnx");
      return;
    }

    setUploading(true);
    try {
      const token = getToken();
      const formData = new FormData();
      formData.append("file", selectedFile);

      const apiBase = getApiBase();
      const res = await fetch(`${apiBase}/api/admin/ranking-model/upload`, {
        method: "POST",
        headers: {
          Authorization: `Bearer ${token}`,
        },
        body: formData,
      });

      if (!res.ok) {
        const errJson = await res.json().catch(() => ({}));
        throw new Error(errJson.error || `HTTP ${res.status}`);
      }

      const updatedStatus: ModelStatus = await res.json();
      setStatus(updatedStatus);
      setSelectedFile(null);
      if (fileInputRef.current) fileInputRef.current.value = "";

      toast.success("Tải lên và kích hoạt mô hình ONNX thành công!");
    } catch (err: any) {
      console.error("Upload error:", err);
      toast.error(err.message || "Tải lên mô hình thất bại.");
    } finally {
      setUploading(false);
    }
  }

  async function handleToggle() {
    setToggling(true);
    try {
      const token = getToken();
      const nextActive = !status?.isActive;
      const res = await api.post<ModelStatus>(
        "/api/admin/ranking-model/toggle",
        { isActive: nextActive },
        token
      );
      setStatus(res);
      toast.success(
        nextActive
          ? "Đã kích hoạt mô hình LambdaMART ONNX thành công!"
          : "Đã tạm dừng mô hình. Hệ thống chuyển về công thức mặc định."
      );
    } catch (err: any) {
      toast.error(err.message || "Lỗi khi kích hoạt/tắt mô hình.");
    } finally {
      setToggling(false);
    }
  }

  async function handleDelete() {
    if (!status?.hasModel) return;
    if (!confirm("Bạn có chắc chắn muốn xóa mô hình ONNX hiện tại? Hệ thống sẽ quay về công thức xếp hạng mặc định.")) {
      return;
    }

    try {
      const token = getToken();
      const res = await api.del<ModelStatus>("/api/admin/ranking-model", token);
      setStatus(res);
      toast.success("Đã xóa mô hình ONNX.");
    } catch (err: any) {
      toast.error(err.message || "Lỗi khi xóa mô hình.");
    }
  }

  function handleDrop(e: React.DragEvent) {
    e.preventDefault();
    setDragOver(false);
    if (e.dataTransfer.files && e.dataTransfer.files.length > 0) {
      const file = e.dataTransfer.files[0];
      if (file.name.toLowerCase().endsWith(".onnx")) {
        setSelectedFile(file);
      } else {
        toast.error("Vui lòng chỉ thả file có đuôi .onnx");
      }
    }
  }

  return (
    <div className="space-y-6 max-w-5xl">
      {/* Page Header */}
      <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-4">
        <div>
          <h1 className="text-2xl font-bold tracking-tight text-foreground flex items-center gap-2.5">
            <Cpu className="h-6 w-6 text-primary" />
            Mô hình Xếp hạng AI (LambdaMART ONNX)
          </h1>
          <p className="text-xs text-muted-foreground mt-1">
            Quản lý và triển khai mô hình học xếp hạng Learning-to-Rank (LTR) cho tìm kiếm và tối ưu hóa điểm hẹn nhóm.
          </p>
        </div>

        <a
          href={`${getApiBase()}/api/admin/ranking-model/sample-template`}
          download="ranking_training_template.csv"
          className="inline-flex items-center gap-2 px-3.5 py-2 rounded-xl border border-border bg-card text-foreground hover:bg-muted text-xs font-semibold shadow-xs transition"
          title="Tải template CSV để huấn luyện"
        >
          <Download className="h-4 w-4 text-primary" />
          <span>Tải CSV Template Train</span>
        </a>
      </div>

      {/* Model Status Card */}
      <Card className="border-border">
        <CardHeader className="pb-3">
          <div className="flex items-center justify-between">
            <CardTitle className="text-base font-bold flex items-center gap-2">
              <Sparkles className="h-4 w-4 text-primary" />
              Trạng thái mô hình hiện tại
            </CardTitle>

            <div className="flex items-center gap-2">
              {loading ? (
                <Loader2 className="h-4 w-4 animate-spin text-muted-foreground" />
              ) : status?.hasModel ? (
                status.isActive ? (
                  <Badge className="bg-emerald-500/15 text-emerald-600 hover:bg-emerald-500/20 border-emerald-500/30 gap-1.5 py-1">
                    <CheckCircle2 className="h-3.5 w-3.5" />
                    Đang hoạt động (Active)
                  </Badge>
                ) : (
                  <Badge variant="outline" className="bg-amber-500/10 text-amber-600 border-amber-500/30 gap-1.5 py-1">
                    <AlertCircle className="h-3.5 w-3.5" />
                    Tạm tắt (Dùng công thức mặc định)
                  </Badge>
                )
              ) : (
                <Badge variant="secondary" className="gap-1.5 py-1 text-muted-foreground">
                  <Info className="h-3.5 w-3.5" />
                  Chưa kích hoạt (Dùng Heuristic)
                </Badge>
              )}

              <Button
                type="button"
                size="icon"
                variant="outline"
                className="h-7 w-7 text-muted-foreground hover:text-foreground"
                title="Làm mới trạng thái từ server"
                onClick={fetchStatus}
                disabled={loading}
              >
                <RefreshCw className={`h-3.5 w-3.5 ${loading ? "animate-spin" : ""}`} />
              </Button>
            </div>
          </div>
          <CardDescription className="text-xs">
            {status?.statusMessage || "Đang kiểm tra trạng thái mô hình..."}
          </CardDescription>
        </CardHeader>

        <CardContent>
          <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-4 gap-4 p-4 rounded-xl bg-muted/40 border border-border/60 text-xs">
            <div>
              <span className="text-muted-foreground block text-[11px]">Tên tệp mô hình:</span>
              <span className="font-semibold text-foreground truncate block mt-0.5">
                {status?.modelName || (status?.hasModel ? "ranking_lambdamart.onnx" : "—")}
              </span>
            </div>

            <div>
              <span className="text-muted-foreground block text-[11px]">Kích thước file:</span>
              <span className="font-semibold text-foreground block mt-0.5">
                {status?.fileSizeBytes ? `${(status.fileSizeBytes / 1024).toFixed(1)} KB` : "—"}
              </span>
            </div>

            <div>
              <span className="text-muted-foreground block text-[11px]">Ngày cập nhật:</span>
              <span className="font-semibold text-foreground block mt-0.5">
                {status?.uploadedAt
                  ? new Date(status.uploadedAt).toLocaleString("vi-VN", {
                      hour: "2-digit",
                      minute: "2-digit",
                      day: "2-digit",
                      month: "2-digit",
                      year: "numeric",
                    })
                  : "—"}
              </span>
            </div>

            <div>
              <span className="text-muted-foreground block text-[11px]">Số đặc trưng đầu vào:</span>
              <span className="font-semibold text-foreground block mt-0.5">
                {status?.featureCount ? `${status.featureCount} features (${status.inputName || "tensor"})` : (status?.hasModel ? "10 features" : "—")}
              </span>
            </div>
          </div>

          {/* Model Actions: Nút kích hoạt độc lập với Upload */}
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 mt-4 pt-3 border-t border-border">
            <div className="text-xs text-muted-foreground flex items-center gap-2">
              <span>Tệp mô hình nhận diện:</span>
              <code className="px-2 py-0.5 rounded bg-muted text-[11px] font-mono font-medium text-foreground">
                {status?.modelName || (status?.hasModel ? "ranking_lambdamart.onnx" : "Chưa có file mô hình")}
              </code>
            </div>

            <div className="flex items-center gap-2">
              <Button
                type="button"
                size="sm"
                variant={status?.isActive ? "outline" : "default"}
                onClick={handleToggle}
                disabled={toggling}
                className={`text-xs h-8 ${!status?.isActive ? "bg-emerald-600 hover:bg-emerald-700 text-white shadow-xs font-semibold" : "border-amber-500/40 text-amber-600 hover:bg-amber-500/10"}`}
              >
                {toggling ? (
                  <Loader2 className="h-3.5 w-3.5 mr-1.5 animate-spin" />
                ) : (
                  <Power className="h-3.5 w-3.5 mr-1.5" />
                )}
                {status?.isActive ? "Tạm dừng mô hình AI (Chuyển về Heuristic)" : "Kích hoạt mô hình AI"}
              </Button>

              {status?.hasModel && (
                <Button
                  type="button"
                  size="sm"
                  variant="destructive"
                  onClick={handleDelete}
                  className="text-xs h-8"
                >
                  <Trash2 className="h-3.5 w-3.5 mr-1.5" />
                  Xóa mô hình
                </Button>
              )}
            </div>
          </div>
        </CardContent>
      </Card>

      {/* Upload New Model Card (Tuỳ chọn) */}
      <Card className="border-border">
        <CardHeader className="pb-3">
          <CardTitle className="text-base font-bold flex items-center gap-2">
            <Upload className="h-4 w-4 text-primary" />
            Tải lên mô hình ONNX mới (Tuỳ chọn)
          </CardTitle>
          <CardDescription className="text-xs">
            Chỉ sử dụng khi bạn muốn thay thế hoặc cập nhật file <code>.onnx</code> mới từ máy cá nhân lên server. Nếu file đã có sẵn trên server (qua git commit / kéo mã nguồn), bạn chỉ cần bấm nút <strong>&quot;Kích hoạt mô hình AI&quot;</strong> ở trên.
          </CardDescription>
        </CardHeader>

        <CardContent className="space-y-4">
          <div
            onDragOver={(e) => {
              e.preventDefault();
              setDragOver(true);
            }}
            onDragLeave={() => setDragOver(false)}
            onDrop={handleDrop}
            onClick={() => fileInputRef.current?.click()}
            className={`p-8 border-2 border-dashed rounded-2xl text-center cursor-pointer transition flex flex-col items-center justify-center gap-2.5 ${
              dragOver
                ? "border-primary bg-primary/5"
                : "border-border hover:border-primary/50 hover:bg-muted/30"
            }`}
          >
            <input
              ref={fileInputRef}
              type="file"
              accept=".onnx"
              className="hidden"
              onChange={(e) => {
                if (e.target.files && e.target.files[0]) {
                  setSelectedFile(e.target.files[0]);
                }
              }}
            />

            <div className="flex h-12 w-12 items-center justify-center rounded-2xl bg-primary/10 text-primary">
              <Cpu className="h-6 w-6" />
            </div>

            <div>
              <p className="text-xs font-semibold text-foreground">
                {selectedFile ? selectedFile.name : "Kéo thả file .onnx vào đây hoặc bấm để chọn"}
              </p>
              <p className="text-[11px] text-muted-foreground mt-0.5">
                {selectedFile
                  ? `Dung lượng: ${(selectedFile.size / 1024).toFixed(1)} KB`
                  : "Chấp nhận file mô hình ONNX (dung lượng tối đa 50MB)"}
              </p>
            </div>
          </div>

          <div className="flex justify-end">
            <Button
              type="button"
              onClick={handleUpload}
              disabled={!selectedFile || uploading}
              className="text-xs font-semibold h-9"
            >
              {uploading ? (
                <>
                  <Loader2 className="h-4 w-4 mr-1.5 animate-spin" />
                  Đang kiểm tra & tải lên...
                </>
              ) : (
                <>
                  <Upload className="h-4 w-4 mr-1.5" />
                  Tải lên file mô hình mới
                </>
              )}
            </Button>
          </div>
        </CardContent>
      </Card>

      {/* Guide & Spec Card */}
      <Card className="border-border">
        <CardHeader className="pb-3">
          <CardTitle className="text-base font-bold flex items-center gap-2">
            <FileCode className="h-4 w-4 text-primary" />
            Đặc tả kỹ thuật cho người làm AI (Google Colab Export)
          </CardTitle>
          <CardDescription className="text-xs">
            Quy chuẩn 10 đặc trưng (Feature Vector) đầu vào mà mô hình LambdaMART cần khớp khi xuất sang ONNX.
          </CardDescription>
        </CardHeader>

        <CardContent className="space-y-4 text-xs">
          <div>
            <h4 className="font-semibold text-foreground mb-2 flex items-center gap-1.5">
              <FileText className="h-3.5 w-3.5 text-primary" />
              Thứ tự 10 đặc trưng (Input Tensor Shape: <code>[batch_size, 10]</code>)
            </h4>
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-2.5 text-[11px]">
              <div className="p-2.5 rounded-lg bg-muted/60 border border-border/50">
                <span className="font-bold text-foreground">1. bm25_norm:</span>
                <span className="text-muted-foreground ml-1.5">Mức khớp nội dung truy vấn (0–1)</span>
              </div>
              <div className="p-2.5 rounded-lg bg-muted/60 border border-border/50">
                <span className="font-bold text-foreground">2. category_match:</span>
                <span className="text-muted-foreground ml-1.5">Khớp loại địa điểm hoặc nhu cầu (1 hoặc 0)</span>
              </div>
              <div className="p-2.5 rounded-lg bg-muted/60 border border-border/50">
                <span className="font-bold text-foreground">3. duration_avg_min:</span>
                <span className="text-muted-foreground ml-1.5">Thời gian trung bình của nhóm (phút)</span>
              </div>
              <div className="p-2.5 rounded-lg bg-muted/60 border border-border/50">
                <span className="font-bold text-foreground">4. duration_max_min:</span>
                <span className="text-muted-foreground ml-1.5">Thời gian của người đi lâu nhất (phút)</span>
              </div>
              <div className="p-2.5 rounded-lg bg-muted/60 border border-border/50">
                <span className="font-bold text-foreground">5. duration_spread_min:</span>
                <span className="text-muted-foreground ml-1.5">Chênh lệch thời gian lớn nhất – nhỏ nhất (phút)</span>
              </div>
              <div className="p-2.5 rounded-lg bg-muted/60 border border-border/50">
                <span className="font-bold text-foreground">6. distance_avg_km:</span>
                <span className="text-muted-foreground ml-1.5">Quãng đường trung bình (km)</span>
              </div>
              <div className="p-2.5 rounded-lg bg-muted/60 border border-border/50">
                <span className="font-bold text-foreground">7. distance_max_km:</span>
                <span className="text-muted-foreground ml-1.5">Quãng đường của người đi xa nhất (km)</span>
              </div>
              <div className="p-2.5 rounded-lg bg-muted/60 border border-border/50">
                <span className="font-bold text-foreground">8. duration_avg_rank_norm:</span>
                <span className="text-muted-foreground ml-1.5">Hạng thời gian trung bình trong query (0–1)</span>
              </div>
              <div className="p-2.5 rounded-lg bg-muted/60 border border-border/50">
                <span className="font-bold text-foreground">9. duration_spread_rank_norm:</span>
                <span className="text-muted-foreground ml-1.5">Hạng chênh lệch thời gian trong query (0–1)</span>
              </div>
              <div className="p-2.5 rounded-lg bg-muted/60 border border-border/50">
                <span className="font-bold text-foreground">10. participant_count:</span>
                <span className="text-muted-foreground ml-1.5">Số người trong nhóm</span>
              </div>
            </div>
          </div>

          <div>
            <h4 className="font-semibold text-foreground mb-1.5">
              Đoạn mã mẫu xuất ONNX trên Google Colab (Python):
            </h4>
            <pre className="p-3.5 rounded-xl bg-slate-950 text-slate-100 font-mono text-[11px] overflow-x-auto leading-relaxed border border-slate-800">
{`# 1. Cài đặt thư viện xuất ONNX trên Colab
!pip install onnxmltools skl2onnx lightgbm

# 2. Sau khi train model LambdaMART (LGBMRanker) xong với 10 features trên, xuất sang ONNX:
import onnxmltools
from onnxmltools.convert.common.data_types import FloatTensorType

# Khai báo input tensor 10 features:
initial_types = [('float_input', FloatTensorType([None, 10]))]
onnx_model = onnxmltools.convert_lightgbm(model, initial_types=initial_types)

# 3. Lưu file và tải về máy
onnxmltools.utils.save_model(onnx_model, 'ranking_lambdamart.onnx')
print("Xuất file ranking_lambdamart.onnx thành công!")`}
            </pre>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
