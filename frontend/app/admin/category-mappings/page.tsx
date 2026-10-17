"use client";

import { useEffect, useState, useMemo, useRef } from "react";
import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
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
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { api, getApiBase } from "@/lib/api";
import { getToken } from "@/lib/auth";
import {
  Tags,
  Plus,
  Trash2,
  RotateCcw,
  Search,
  CheckCircle2,
  XCircle,
  FlaskConical,
  Sparkles,
  Layers,
  ArrowRight,
  ArrowLeft,
  Loader2,
  X,
  Pencil,
  RefreshCw,
  FolderTree,
  Bot,
  Zap,
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Database,
  Tag,
  Check,
} from "lucide-react";
import { toast } from "sonner";

interface CategoryMappingConfig {
  categoryGroups: Record<string, string[]>;
  specificOverrides: Record<string, string>;
}

const COLAB_GROUPS = [
  "cafe",
  "viet",
  "japanese",
  "korean",
  "thai",
  "seafood",
  "western",
  "vegetarian",
  "chinese",
  "bbq_hotpot",
  "bookstore",
  "library",
  "park",
];

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

export default function CategoryMappingsPage() {
  const [config, setConfig] = useState<CategoryMappingConfig>({
    categoryGroups: {},
    specificOverrides: {},
  });
  const [loading, setLoading] = useState(true);

  // Distinct categories from DB (/api/places/categories)
  const [dbCategories, setDbCategories] = useState<string[]>([]);
  const [loadingDbCategories, setLoadingDbCategories] = useState(false);

  // Active view: main list vs detail of a specific group
  const [selectedGroup, setSelectedGroup] = useState<string | null>(null);

  // Tabs: "groups" | "overrides" | "simulator"
  const [activeTab, setActiveTab] = useState<"groups" | "overrides" | "simulator">("groups");

  // Filters for Main Groups list
  const [searchQuery, setSearchQuery] = useState("");
  const [groupPage, setGroupPage] = useState(1);
  const [groupPageSize, setGroupPageSize] = useState(10);

  // Filters for Overrides
  const [overridePage, setOverridePage] = useState(1);
  const [overridePageSize, setOverridePageSize] = useState(10);

  // Detail View State (inside a selected group)
  const [detailSearch, setDetailSearch] = useState("");
  const [detailPage, setDetailPage] = useState(1);
  const [detailPageSize, setDetailPageSize] = useState(10);

  // Autocomplete / Dropdown for adding category in Detail View
  const [addCategoryInput, setAddCategoryInput] = useState("");
  const [isDropdownOpen, setIsDropdownOpen] = useState(false);
  const dropdownRef = useRef<HTMLDivElement>(null);

  // Rename Group Modal
  const [isRenameOpen, setIsRenameOpen] = useState(false);
  const [renameNewName, setRenameNewName] = useState("");
  const [isRenaming, setIsRenaming] = useState(false);

  // Add Group Modal
  const [isAddGroupOpen, setIsAddGroupOpen] = useState(false);
  const [newGroupName, setNewGroupName] = useState("");
  const [newGroupCategories, setNewGroupCategories] = useState("");
  const [isSubmittingGroup, setIsSubmittingGroup] = useState(false);

  // Add Override Modal
  const [isAddOverrideOpen, setIsAddOverrideOpen] = useState(false);
  const [newOverrideKeyword, setNewOverrideKeyword] = useState("");
  const [newOverrideTarget, setNewOverrideTarget] = useState("");
  const [isSubmittingOverride, setIsSubmittingOverride] = useState(false);

  // Reset Dialog
  const [isResetConfirmOpen, setIsResetConfirmOpen] = useState(false);
  const [isResetting, setIsResetting] = useState(false);

  // Simulator
  const [testPlaceCategory, setTestPlaceCategory] = useState("sushi_restaurant");
  const [testTargetCategory, setTestTargetCategory] = useState("japanese");
  const [testSpecificPreference, setTestSpecificPreference] = useState("sushi");
  const [testRawQuery, setTestRawQuery] = useState("tìm quán sushi ngon quận 1");
  const [testResult, setTestResult] = useState<{
    placeCategory?: string;
    targetCategory?: string;
    specificPreference?: string;
    query?: string;
    categoryMatchScore: number;
    isMatch: boolean;
  } | null>(null);
  const [isTesting, setIsTesting] = useState(false);

  // Load config from backend
  async function loadConfig() {
    setLoading(true);
    try {
      const token = getToken();
      const res = await api.get<CategoryMappingConfig>("/api/admin/category-mappings", token);
      setConfig({
        categoryGroups: res.categoryGroups || {},
        specificOverrides: res.specificOverrides || {},
      });
    } catch (err: unknown) {
      console.error("Error loading category mappings:", err);
      toast.error("Không thể tải cấu hình Category Mappings.");
    } finally {
      setLoading(false);
    }
  }

  // Load distinct categories from DB
  async function loadDbCategories() {
    setLoadingDbCategories(true);
    try {
      const token = getToken();
      const cats = await api.get<string[]>("/api/places/categories", token);
      if (Array.isArray(cats)) {
        setDbCategories(cats.filter(Boolean));
      }
    } catch (err) {
      console.warn("Could not load distinct categories from /api/places/categories", err);
    } finally {
      setLoadingDbCategories(false);
    }
  }

  useEffect(() => {
    void loadConfig();
    void loadDbCategories();
  }, []);

  // Sync selectedGroup with URL parameter if present
  useEffect(() => {
    if (typeof window !== "undefined") {
      const params = new URLSearchParams(window.location.search);
      const grp = params.get("group");
      if (grp && config.categoryGroups[grp]) {
        setSelectedGroup(grp);
      }
    }
  }, [config.categoryGroups]);

  // Click outside to close autocomplete dropdown
  useEffect(() => {
    function handleClickOutside(event: MouseEvent) {
      if (dropdownRef.current && !dropdownRef.current.contains(event.target as Node)) {
        setIsDropdownOpen(false);
      }
    }
    document.addEventListener("mousedown", handleClickOutside);
    return () => document.removeEventListener("mousedown", handleClickOutside);
  }, []);

  const totalGroups = useMemo(() => Object.keys(config.categoryGroups).length, [config.categoryGroups]);
  const totalCategories = useMemo(
    () => Object.values(config.categoryGroups).reduce((acc, curr) => acc + (curr?.length || 0), 0),
    [config.categoryGroups]
  );
  const totalOverrides = useMemo(() => Object.keys(config.specificOverrides).length, [config.specificOverrides]);

  // Filter groups for Main Tab
  const filteredGroups = useMemo(() => {
    const entries = Object.entries(config.categoryGroups);
    if (!searchQuery.trim()) return entries;
    const q = searchQuery.toLowerCase().trim();
    return entries.filter(([grp, cats]) => {
      if (grp.toLowerCase().includes(q)) return true;
      return cats.some((c) => c.toLowerCase().includes(q));
    });
  }, [config.categoryGroups, searchQuery]);

  const totalGroupPages = useMemo(() => {
    return Math.max(1, Math.ceil(filteredGroups.length / groupPageSize));
  }, [filteredGroups.length, groupPageSize]);

  const paginatedGroups = useMemo(() => {
    const start = (groupPage - 1) * groupPageSize;
    return filteredGroups.slice(start, start + groupPageSize);
  }, [filteredGroups, groupPage, groupPageSize]);

  // Overrides list
  const filteredOverrides = useMemo(() => {
    const entries = Object.entries(config.specificOverrides);
    if (!searchQuery.trim()) return entries;
    const q = searchQuery.toLowerCase().trim();
    return entries.filter(([kw, target]) => {
      return kw.toLowerCase().includes(q) || target.toLowerCase().includes(q);
    });
  }, [config.specificOverrides, searchQuery]);

  const totalOverridePages = useMemo(() => {
    return Math.max(1, Math.ceil(filteredOverrides.length / overridePageSize));
  }, [filteredOverrides.length, overridePageSize]);

  const paginatedOverrides = useMemo(() => {
    const start = (overridePage - 1) * overridePageSize;
    return filteredOverrides.slice(start, start + overridePageSize);
  }, [filteredOverrides, overridePage, overridePageSize]);

  // Current selected group categories
  const currentGroupCategories = useMemo(() => {
    if (!selectedGroup) return [];
    return config.categoryGroups[selectedGroup] || [];
  }, [config.categoryGroups, selectedGroup]);

  // Filter categories in Detail View
  const filteredDetailCategories = useMemo(() => {
    if (!detailSearch.trim()) return currentGroupCategories;
    const q = detailSearch.toLowerCase().trim();
    return currentGroupCategories.filter((c) => c.toLowerCase().includes(q));
  }, [currentGroupCategories, detailSearch]);

  const totalDetailPages = useMemo(() => {
    return Math.max(1, Math.ceil(filteredDetailCategories.length / detailPageSize));
  }, [filteredDetailCategories.length, detailPageSize]);

  const paginatedDetailCategories = useMemo(() => {
    const start = (detailPage - 1) * detailPageSize;
    return filteredDetailCategories.slice(start, start + detailPageSize);
  }, [filteredDetailCategories, detailPage, detailPageSize]);

  // Autocomplete suggestions from DB (excluding already mapped categories)
  const dbCategorySuggestions = useMemo(() => {
    const currentSet = new Set(currentGroupCategories.map((c) => c.toLowerCase()));
    const q = addCategoryInput.toLowerCase().trim();
    if (!q) {
      // Khi ô nhập trống: ưu tiên các category từ DB có liên quan đến tên nhóm
      const groupKeyword = selectedGroup?.toLowerCase() ?? "";
      const prioritized = dbCategories.filter(
        (cat) => !currentSet.has(cat.toLowerCase()) && cat.toLowerCase().includes(groupKeyword)
      );
      const others = dbCategories.filter(
        (cat) => !currentSet.has(cat.toLowerCase()) && !cat.toLowerCase().includes(groupKeyword)
      );
      return [...prioritized, ...others].slice(0, 20);
    }
    return dbCategories
      .filter((cat) => !currentSet.has(cat.toLowerCase()) && cat.toLowerCase().includes(q))
      .slice(0, 25);
  }, [dbCategories, currentGroupCategories, addCategoryInput, selectedGroup]);

  // Navigate to group detail
  function handleSelectGroup(groupName: string) {
    setSelectedGroup(groupName);
    setDetailSearch("");
    setDetailPage(1);
    setAddCategoryInput("");
    setIsDropdownOpen(false);
    if (typeof window !== "undefined") {
      const url = new URL(window.location.href);
      url.searchParams.set("group", groupName);
      window.history.pushState({}, "", url.toString());
    }
  }

  // Back to group list
  function handleBackToList() {
    setSelectedGroup(null);
    if (typeof window !== "undefined") {
      const url = new URL(window.location.href);
      url.searchParams.delete("group");
      window.history.pushState({}, "", url.toString());
    }
  }

  // Handle Add Category to Group
  async function handleAddCategoryToCurrentGroup(category: string) {
    if (!selectedGroup) return;
    const cat = category.trim().toLowerCase();
    if (!cat) {
      toast.error("Vui lòng nhập hoặc chọn tên category Overture.");
      return;
    }

    if (currentGroupCategories.includes(cat)) {
      toast.error(`Category "${cat}" đã có trong nhóm "${selectedGroup}".`);
      return;
    }

    try {
      const token = getToken();
      await api.post(
        `/api/admin/category-mappings/groups/${encodeURIComponent(selectedGroup)}/categories`,
        { category: cat },
        token
      );
      toast.success(`Đã thêm "${cat}" vào nhóm "${selectedGroup}".`);
      setAddCategoryInput("");
      setIsDropdownOpen(false);
      await loadConfig();
    } catch (err: any) {
      toast.error(err.message || "Không thể thêm category.");
    }
  }

  // Handle Remove Category from Group
  async function handleRemoveCategory(groupName: string, category: string) {
    try {
      const token = getToken();
      const apiBase = getApiBase();
      const res = await fetch(
        `${apiBase}/api/admin/category-mappings/groups/${encodeURIComponent(groupName)}/categories/${encodeURIComponent(category)}`,
        {
          method: "DELETE",
          headers: { Authorization: `Bearer ${token}` },
        }
      );

      if (!res.ok) {
        const errJson = await res.json().catch(() => ({}));
        throw new Error(errJson.error || "Xoá category thất bại");
      }

      toast.success(`Đã loại bỏ "${category}" khỏi "${groupName}".`);
      await loadConfig();
    } catch (err: any) {
      toast.error(err.message || "Không thể xoá category.");
    }
  }

  // Handle Rename Group
  async function handleRenameGroup(e: React.FormEvent) {
    e.preventDefault();
    if (!selectedGroup) return;
    const newName = renameNewName.trim().toLowerCase();
    if (!newName) {
      toast.error("Vui lòng nhập tên nhóm mới.");
      return;
    }

    if (newName === selectedGroup) {
      setIsRenameOpen(false);
      return;
    }

    setIsRenaming(true);
    try {
      const token = getToken();
      const currentCats = config.categoryGroups[selectedGroup] || [];

      // 1. Tạo nhóm mới với tên mới và toàn bộ categories hiện có
      await api.post(
        "/api/admin/category-mappings/groups",
        { groupName: newName, categories: currentCats },
        token
      );

      // 2. Xoá nhóm cũ
      const apiBase = getApiBase();
      await fetch(`${apiBase}/api/admin/category-mappings/groups/${encodeURIComponent(selectedGroup)}`, {
        method: "DELETE",
        headers: { Authorization: `Bearer ${token}` },
      });

      toast.success(`Đã đổi tên nhóm từ "${selectedGroup}" thành "${newName}".`);
      setSelectedGroup(newName);
      setIsRenameOpen(false);
      await loadConfig();
    } catch (err: any) {
      toast.error(err.message || "Đổi tên nhóm thất bại.");
    } finally {
      setIsRenaming(false);
    }
  }

  // Handle Create Group
  async function handleAddGroup(e: React.FormEvent) {
    e.preventDefault();
    const groupName = newGroupName.trim().toLowerCase();
    if (!groupName) {
      toast.error("Vui lòng nhập mã định danh nhóm (ví dụ: cafe, cinema, spa).");
      return;
    }

    const categories = newGroupCategories
      .split(/[,\n]/)
      .map((c) => c.trim().toLowerCase())
      .filter(Boolean);

    setIsSubmittingGroup(true);
    try {
      const token = getToken();
      await api.post(
        "/api/admin/category-mappings/groups",
        { groupName, categories },
        token
      );
      toast.success(`Đã thêm nhóm "${groupName}" thành công.`);
      setIsAddGroupOpen(false);
      setNewGroupName("");
      setNewGroupCategories("");
      await loadConfig();
    } catch (err: any) {
      toast.error(err.message || "Không thể tạo nhóm mới.");
    } finally {
      setIsSubmittingGroup(false);
    }
  }

  // Handle Delete Group
  async function handleDeleteGroup(groupName: string) {
    if (!confirm(`Bạn có chắc chắn muốn xoá nhóm "${groupName}" và toàn bộ categories liên kết?`)) {
      return;
    }

    try {
      const token = getToken();
      const apiBase = getApiBase();
      const res = await fetch(`${apiBase}/api/admin/category-mappings/groups/${encodeURIComponent(groupName)}`, {
        method: "DELETE",
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!res.ok) {
        const errJson = await res.json().catch(() => ({}));
        throw new Error(errJson.error || "Xoá nhóm thất bại");
      }

      toast.success(`Đã xoá nhóm "${groupName}".`);
      if (selectedGroup === groupName) {
        handleBackToList();
      }
      await loadConfig();
    } catch (err: any) {
      toast.error(err.message || "Xoá nhóm thất bại.");
    }
  }

  // Handle Add Override
  async function handleAddOverride(e: React.FormEvent) {
    e.preventDefault();
    const keyword = newOverrideKeyword.trim().toLowerCase();
    const targetCategory = newOverrideTarget.trim().toLowerCase();

    if (!keyword || !targetCategory) {
      toast.error("Vui lòng điền đầy đủ từ khoá và category mục tiêu.");
      return;
    }

    setIsSubmittingOverride(true);
    try {
      const token = getToken();
      await api.post(
        "/api/admin/category-mappings/overrides",
        { keyword, targetCategory },
        token
      );
      toast.success(`Đã thêm override: "${keyword}" → "${targetCategory}".`);
      setIsAddOverrideOpen(false);
      setNewOverrideKeyword("");
      setNewOverrideTarget("");
      await loadConfig();
    } catch (err: any) {
      toast.error(err.message || "Không thể thêm override.");
    } finally {
      setIsSubmittingOverride(false);
    }
  }

  // Handle Delete Override
  async function handleDeleteOverride(keyword: string) {
    try {
      const token = getToken();
      const apiBase = getApiBase();
      const res = await fetch(`${apiBase}/api/admin/category-mappings/overrides/${encodeURIComponent(keyword)}`, {
        method: "DELETE",
        headers: { Authorization: `Bearer ${token}` },
      });

      if (!res.ok) {
        const errJson = await res.json().catch(() => ({}));
        throw new Error(errJson.error || "Xoá override thất bại");
      }

      toast.success(`Đã xoá override "${keyword}".`);
      await loadConfig();
    } catch (err: any) {
      toast.error(err.message || "Không thể xoá override.");
    }
  }

  // Handle Reset to Defaults
  async function handleResetToDefaults() {
    setIsResetting(true);
    try {
      const token = getToken();
      await api.post("/api/admin/category-mappings/reset", {}, token);
      toast.success("Đã khôi phục cấu hình 13 nhóm và 31 categories chuẩn Colab thành công!");
      setIsResetConfirmOpen(false);
      await loadConfig();
    } catch (err: any) {
      toast.error(err.message || "Không thể khôi phục mặc định.");
    } finally {
      setIsResetting(false);
    }
  }

  // Run Test Match
  async function handleRunTestMatch() {
    setIsTesting(true);
    try {
      const token = getToken();
      const res = await api.post<any>(
        "/api/admin/category-mappings/test-match",
        {
          placeCategory: testPlaceCategory,
          targetCategory: testTargetCategory,
          specificPreference: testSpecificPreference,
          query: testRawQuery,
        },
        token
      );
      setTestResult(res);
    } catch (err: any) {
      toast.error(err.message || "Lỗi khi kiểm tra khớp thể loại.");
    } finally {
      setIsTesting(false);
    }
  }

  return (
    <div className="space-y-6 pb-28">
      {/* Page Header */}
      <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-2xl font-bold tracking-tight flex items-center gap-2.5">
            <Tags className="h-6 w-6 text-primary" />
            Quản lý Phân nhóm Thể loại (Category Mappings)
          </h2>
          <p className="text-sm text-muted-foreground">
            Quản trị 13 nhóm danh mục huấn luyện AI (LambdaMART), ánh xạ Overture POI và các quy tắc đặc tả món.
          </p>
        </div>

        <div className="flex items-center gap-2">
          <Button
            variant="outline"
            size="sm"
            onClick={() => void loadConfig()}
            disabled={loading}
          >
            <RefreshCw className={`mr-1.5 h-3.5 w-3.5 ${loading ? "animate-spin" : ""}`} />
            Làm mới
          </Button>

          <Button
            variant="outline"
            size="sm"
            onClick={() => setIsResetConfirmOpen(true)}
            className="text-amber-600 hover:text-amber-700 hover:bg-amber-500/10 border-amber-300 dark:border-amber-800"
          >
            <RotateCcw className="mr-1.5 h-3.5 w-3.5" />
            Khôi phục chuẩn Colab
          </Button>

          {!selectedGroup && activeTab === "groups" && (
            <Button size="sm" onClick={() => setIsAddGroupOpen(true)}>
              <Plus className="mr-1.5 h-3.5 w-3.5" />
              Thêm nhóm mới
            </Button>
          )}

          {activeTab === "overrides" && (
            <Button size="sm" onClick={() => setIsAddOverrideOpen(true)}>
              <Plus className="mr-1.5 h-3.5 w-3.5" />
              Thêm Override
            </Button>
          )}
        </div>
      </div>

      {/* Metrics Row (Chỉ hiển thị ở màn hình chính, ẩn khi đang ở trang chi tiết nhóm) */}
      {!selectedGroup && (
        <div className="grid gap-4 sm:grid-cols-3">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-xs font-medium text-muted-foreground uppercase tracking-wider">
              Tổng số Nhóm (Groups)
            </CardTitle>
            <Layers className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {loading ? "…" : totalGroups}
            </div>
            <p className="text-xs text-muted-foreground mt-1 flex items-center gap-1.5">
              <span className="inline-block w-2 h-2 rounded-full bg-emerald-500" />
              13 nhóm chuẩn học máy (Colab)
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-xs font-medium text-muted-foreground uppercase tracking-wider">
              Categories Overture Đã Map
            </CardTitle>
            <FolderTree className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {loading ? "…" : totalCategories}
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              Phân loại dữ liệu POI vào miền hiểu AI
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
            <CardTitle className="text-xs font-medium text-muted-foreground uppercase tracking-wider">
              Quy tắc Ghi đè (Overrides)
            </CardTitle>
            <Sparkles className="h-4 w-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-2xl font-bold">
              {loading ? "…" : totalOverrides}
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              Ưu tiên đặc tả món ăn &amp; phong cách riêng
            </p>
          </CardContent>
        </Card>
      </div>
      )}

      {/* Navigation Sub-Tabs (Chỉ hiển thị ở màn hình chính, ẩn khi đang ở trang chi tiết nhóm) */}
      {!selectedGroup && (
        <div className="border-b flex items-center gap-6 text-sm font-medium">
          <button
            type="button"
            onClick={() => {
              setActiveTab("groups");
              setSelectedGroup(null);
            }}
            className={`pb-3 relative transition-colors ${
              activeTab === "groups"
                ? "text-primary font-semibold border-b-2 border-primary"
                : "text-muted-foreground hover:text-foreground"
            }`}
          >
            Nhóm Thể loại (Category Groups)
            <span className="ml-2 rounded-full bg-muted px-2 py-0.5 text-xs font-normal">
              {totalGroups}
            </span>
          </button>

          <button
            type="button"
            onClick={() => {
              setActiveTab("overrides");
              setSelectedGroup(null);
            }}
            className={`pb-3 relative transition-colors ${
              activeTab === "overrides"
                ? "text-primary font-semibold border-b-2 border-primary"
                : "text-muted-foreground hover:text-foreground"
            }`}
          >
            Quy tắc Đặc tả (Specific Overrides)
            <span className="ml-2 rounded-full bg-muted px-2 py-0.5 text-xs font-normal">
              {totalOverrides}
            </span>
          </button>

          <button
            type="button"
            onClick={() => {
              setActiveTab("simulator");
              setSelectedGroup(null);
            }}
            className={`pb-3 relative transition-colors flex items-center gap-1.5 ${
              activeTab === "simulator"
                ? "text-primary font-semibold border-b-2 border-primary"
                : "text-muted-foreground hover:text-foreground"
            }`}
          >
            <FlaskConical className="h-3.5 w-3.5" />
            Kiểm thử Khớp (Match Simulator)
          </button>
        </div>
      )}

      {/* ======================================================== */}
      {/* TAB 1: CATEGORY GROUPS - MAIN TABLE VS DETAIL VIEW      */}
      {/* ======================================================== */}
      {activeTab === "groups" && !selectedGroup && (
        <div className="space-y-4">
          {/* Toolbar */}
          <Card>
            <CardContent className="pt-5">
              <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                <div className="relative flex-1 max-w-md">
                  <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
                  <Input
                    type="text"
                    placeholder="Tìm theo mã nhóm..."
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    className="pl-9 text-sm"
                  />
                </div>

                <div className="flex items-center gap-2 text-xs text-muted-foreground">
                  <span>
                    Hiển thị <strong>{filteredGroups.length}</strong> / {totalGroups} nhóm
                  </span>
                  {searchQuery && (
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setSearchQuery("")}
                      className="h-8 px-2 text-xs"
                    >
                      <X className="h-3 w-3 mr-1" /> Xoá lọc
                    </Button>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>

          {/* Table Container (CLEAN: Bỏ cột categories overture, chỉ để icon bút chì không chữ) */}
          <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
              <div>
                <CardTitle className="text-base font-semibold">
                  Danh mục Nhóm Phân loại AI
                </CardTitle>
                <CardDescription className="text-xs">
                  Nhấp vào biểu tượng chỉnh sửa để xem chi tiết, chỉnh sửa tên và danh sách Category Overture liên kết.
                </CardDescription>
              </div>
            </CardHeader>

            <CardContent className="p-0">
              <div className="relative overflow-x-auto max-h-[calc(100vh-360px)] min-h-[340px] overflow-y-auto">
                <Table>
                  <TableHeader className="sticky top-0 z-10 bg-muted/90 backdrop-blur shadow-xs">
                    <TableRow>
                      <TableHead className="w-12 text-center">#</TableHead>
                      <TableHead>Mã nhóm (Group Key)</TableHead>
                      <TableHead className="w-[140px] text-center">Số categories</TableHead>
                      <TableHead className="w-[100px] text-right">Thao tác</TableHead>
                    </TableRow>
                  </TableHeader>

                  <TableBody>
                    {loading ? (
                      <TableRow>
                        <TableCell colSpan={4} className="h-36 text-center text-sm text-muted-foreground">
                          <div className="flex items-center justify-center gap-2">
                            <Loader2 className="h-4 w-4 animate-spin text-primary" />
                            <span>Đang nạp dữ liệu nhóm...</span>
                          </div>
                        </TableCell>
                      </TableRow>
                    ) : paginatedGroups.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={4} className="h-36 text-center text-sm text-muted-foreground">
                          Không tìm thấy nhóm nào phù hợp với từ khoá.
                        </TableCell>
                      </TableRow>
                    ) : (
                      paginatedGroups.map(([groupName, categories], idx) => {
                        const globalIndex = (groupPage - 1) * groupPageSize + idx + 1;
                        const isColab = COLAB_GROUPS.includes(groupName);
                        return (
                          <TableRow
                            key={groupName}
                            className="hover:bg-muted/40 cursor-pointer"
                            onClick={() => handleSelectGroup(groupName)}
                          >
                            <TableCell className="text-center font-mono text-xs text-muted-foreground">
                              {globalIndex}
                            </TableCell>

                            <TableCell>
                              <div className="flex items-center gap-2">
                                <span className="font-mono font-semibold text-sm text-foreground">
                                  {groupName}
                                </span>
                                {isColab && (
                                  <Badge
                                    variant="outline"
                                    className="text-[10px] py-0 px-1.5 border-emerald-500/40 text-emerald-600 dark:text-emerald-400 bg-emerald-500/10"
                                  >
                                    Colab
                                  </Badge>
                                )}
                              </div>
                            </TableCell>

                            <TableCell className="text-center">
                              <Badge variant="secondary" className="font-mono text-xs">
                                {categories?.length || 0}
                              </Badge>
                            </TableCell>

                            <TableCell className="text-right" onClick={(e) => e.stopPropagation()}>
                              <div className="flex items-center justify-end gap-1">
                                <Button
                                  size="icon"
                                  variant="ghost"
                                  className="h-8 w-8 text-muted-foreground hover:text-foreground"
                                  title="Xem chi tiết & Quản lý"
                                  onClick={() => handleSelectGroup(groupName)}
                                >
                                  <Pencil className="h-3.5 w-3.5" />
                                </Button>

                                <Button
                                  size="icon"
                                  variant="ghost"
                                  className="h-8 w-8 text-muted-foreground hover:text-destructive"
                                  title="Xoá nhóm"
                                  onClick={() => handleDeleteGroup(groupName)}
                                >
                                  <Trash2 className="h-3.5 w-3.5" />
                                </Button>
                              </div>
                            </TableCell>
                          </TableRow>
                        );
                      })
                    )}
                  </TableBody>
                </Table>
              </div>

              {/* Pagination Controls */}
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between border-t px-4 py-3 bg-card">
                <div className="flex flex-wrap items-center gap-4 text-xs text-muted-foreground">
                  <div>
                    Hiển thị{" "}
                    <span className="font-semibold text-foreground">
                      {filteredGroups.length === 0 ? 0 : (groupPage - 1) * groupPageSize + 1}
                    </span>{" "}
                    –{" "}
                    <span className="font-semibold text-foreground">
                      {Math.min(groupPage * groupPageSize, filteredGroups.length)}
                    </span>{" "}
                    trong{" "}
                    <span className="font-semibold text-foreground">
                      {filteredGroups.length}
                    </span>{" "}
                    nhóm
                  </div>

                  <div className="flex items-center gap-1.5">
                    <span>Số dòng:</span>
                    <select
                      value={groupPageSize}
                      onChange={(e) => {
                        setGroupPageSize(Number(e.target.value));
                        setGroupPage(1);
                      }}
                      className="h-8 rounded-md border border-input bg-background px-2 py-1 text-xs shadow-xs focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring text-foreground font-medium cursor-pointer"
                    >
                      <option value={5}>5 / trang</option>
                      <option value={10}>10 / trang</option>
                      <option value={15}>15 / trang</option>
                      <option value={25}>25 / trang</option>
                      <option value={50}>50 / trang</option>
                    </select>
                  </div>
                </div>

                <div className="flex items-center gap-1">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setGroupPage(1)}
                    disabled={groupPage <= 1 || loading}
                    className="h-8 w-8 p-0 text-xs"
                    title="Trang đầu"
                  >
                    <ChevronsLeft className="h-4 w-4" />
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setGroupPage((p) => Math.max(1, p - 1))}
                    disabled={groupPage <= 1 || loading}
                    className="h-8 w-8 p-0 text-xs"
                    title="Trang trước"
                  >
                    <ChevronLeft className="h-4 w-4" />
                  </Button>

                  {getPageNumbers(groupPage, totalGroupPages).map((pNum, idx) =>
                    pNum === "..." ? (
                      <span key={`group-ellipsis-${idx}`} className="px-1 text-xs text-muted-foreground select-none">
                        …
                      </span>
                    ) : (
                      <Button
                        key={`group-page-${pNum}`}
                        variant={groupPage === pNum ? "default" : "outline"}
                        size="sm"
                        onClick={() => setGroupPage(pNum as number)}
                        disabled={loading}
                        className={`h-8 min-w-[32px] px-2 text-xs font-medium ${
                          groupPage === pNum ? "pointer-events-none" : ""
                        }`}
                      >
                        {pNum}
                      </Button>
                    )
                  )}

                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setGroupPage((p) => Math.min(totalGroupPages, p + 1))}
                    disabled={groupPage >= totalGroupPages || loading}
                    className="h-8 w-8 p-0 text-xs"
                    title="Trang sau"
                  >
                    <ChevronRight className="h-4 w-4" />
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setGroupPage(totalGroupPages)}
                    disabled={groupPage >= totalGroupPages || loading}
                    className="h-8 w-8 p-0 text-xs"
                    title="Trang cuối"
                  >
                    <ChevronsRight className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
      )}

      {/* ======================================================== */}
      {/* DETAIL VIEW: CHI TIẾT NHÓM & QUẢN LÝ CATEGORIES OVERTURE */}
      {/* ======================================================== */}
      {activeTab === "groups" && selectedGroup && (
        <div className="space-y-5">
          {/* Top Bar Navigation */}
          <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 bg-card border rounded-xl p-4 shadow-2xs">
            <div className="flex items-center gap-3">
              <Button
                variant="outline"
                size="sm"
                onClick={handleBackToList}
                className="gap-1.5 h-8 text-xs font-medium"
              >
                <ArrowLeft className="h-3.5 w-3.5" />
                Quay lại danh sách nhóm
              </Button>

              <div className="h-4 w-[1px] bg-border" />

              <div className="flex items-center gap-2">
                <span className="text-xs text-muted-foreground">Mã nhóm:</span>
                <span className="font-mono text-base font-bold text-foreground">
                  {selectedGroup}
                </span>
                {COLAB_GROUPS.includes(selectedGroup) && (
                  <Badge variant="outline" className="text-[10px] py-0 px-1.5 border-emerald-500/40 text-emerald-600 bg-emerald-500/10">
                    Colab Notebook
                  </Badge>
                )}
                <Badge variant="secondary" className="font-mono text-xs">
                  {currentGroupCategories.length} categories
                </Badge>
              </div>
            </div>

            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                className="h-8 text-xs gap-1.5"
                onClick={() => {
                  setRenameNewName(selectedGroup);
                  setIsRenameOpen(true);
                }}
              >
                <Pencil className="h-3.5 w-3.5" />
                Đổi tên nhóm
              </Button>

              <Button
                variant="ghost"
                size="sm"
                className="h-8 text-xs text-destructive hover:text-destructive hover:bg-destructive/10 gap-1.5"
                onClick={() => handleDeleteGroup(selectedGroup)}
              >
                <Trash2 className="h-3.5 w-3.5" />
                Xoá nhóm này
              </Button>
            </div>
          </div>

          {/* Card: Thêm Category mới với Dropdown Autocomplete từ CSDL */}
          <Card className="overflow-visible relative z-30">
            <CardHeader className="pb-3">
              <CardTitle className="text-sm font-semibold flex items-center gap-2">
                <Plus className="h-4 w-4 text-primary" />
                Thêm Category Overture vào nhóm &quot;{selectedGroup}&quot;
              </CardTitle>
              <CardDescription className="text-xs">
                Nhập hoặc chọn danh mục thực tế từ cơ sở dữ liệu ({dbCategories.length} categories sẵn có).
              </CardDescription>
            </CardHeader>

            <CardContent className="overflow-visible">
              <div className="relative max-w-xl" ref={dropdownRef}>
                <div className="flex gap-2">
                  <div className="relative flex-1">
                    <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
                    <Input
                      type="text"
                      name="overture_category_input_no_autofill"
                      id="overture_category_input_no_autofill"
                      autoComplete="off"
                      autoCorrect="off"
                      autoCapitalize="off"
                      spellCheck={false}
                      placeholder="Aa"
                      value={addCategoryInput}
                      onChange={(e) => {
                        setAddCategoryInput(e.target.value);
                        setIsDropdownOpen(true);
                      }}
                      onFocus={() => setIsDropdownOpen(true)}
                      onKeyDown={(e) => {
                        if (e.key === "Enter") {
                          e.preventDefault();
                          void handleAddCategoryToCurrentGroup(addCategoryInput);
                        }
                      }}
                      className="pl-9 font-mono text-xs"
                    />
                    {addCategoryInput && (
                      <button
                        type="button"
                        onClick={() => {
                          setAddCategoryInput("");
                          setIsDropdownOpen(false);
                        }}
                        className="absolute right-2.5 top-2.5 text-muted-foreground hover:text-foreground"
                      >
                        <X className="h-3.5 w-3.5" />
                      </button>
                    )}
                  </div>

                  <Button
                    size="sm"
                    onClick={() => void handleAddCategoryToCurrentGroup(addCategoryInput)}
                    className="gap-1.5 text-xs"
                  >
                    <Plus className="h-3.5 w-3.5" /> Thêm vào nhóm
                  </Button>
                </div>

                {/* Dropdown Gợi ý từ DB */}
                {isDropdownOpen && (
                  <div className="absolute left-0 right-0 top-full z-50 mt-1 max-h-72 w-full overflow-y-auto rounded-lg border bg-popover text-popover-foreground shadow-2xl animate-in fade-in-0 zoom-in-95">
                    <div className="px-3 py-2 text-[11px] font-semibold text-muted-foreground border-b flex items-center justify-between bg-muted/70">
                      <span>Danh mục gợi ý từ Database:</span>
                      <span className="font-mono text-primary font-bold">
                        {dbCategorySuggestions.length} kết quả
                      </span>
                    </div>

                    {loadingDbCategories ? (
                      <div className="p-4 text-center text-xs text-muted-foreground flex items-center justify-center gap-2">
                        <Loader2 className="h-3.5 w-3.5 animate-spin text-primary" />
                        <span>Đang tải danh mục từ CSDL...</span>
                      </div>
                    ) : dbCategorySuggestions.length === 0 ? (
                      <div className="p-4 text-center text-xs text-muted-foreground">
                        {addCategoryInput ? (
                          <>
                            Không tìm thấy category nào khớp với &quot;{addCategoryInput}&quot; trong DB.
                            <p className="mt-1 text-[11px] text-muted-foreground/80">
                              (Bạn vẫn có thể bấm &quot;Thêm vào nhóm&quot; để thêm category tuỳ chỉnh này)
                            </p>
                          </>
                        ) : (
                          "Không còn category nào chưa được map."
                        )}
                      </div>
                    ) : (
                      <ul className="py-1 text-xs font-mono divide-y divide-border/40">
                        {dbCategorySuggestions.map((cat) => (
                          <li
                            key={cat}
                            className="px-3 py-2.5 hover:bg-accent hover:text-accent-foreground cursor-pointer flex items-center justify-between transition-colors"
                            onMouseDown={(e) => {
                              e.preventDefault();
                              void handleAddCategoryToCurrentGroup(cat);
                            }}
                          >
                            <span className="flex items-center gap-2">
                              <Tag className="h-3.5 w-3.5 text-primary" />
                              <span className="font-medium">{cat}</span>
                            </span>
                            <span className="text-[10px] text-primary/90 bg-primary/10 border border-primary/20 px-2 py-0.5 rounded font-sans flex items-center gap-1 font-medium">
                              <Plus className="h-3 w-3" /> Nhấn để thêm
                            </span>
                          </li>
                        ))}
                      </ul>
                    )}
                  </div>
                )}
              </div>
            </CardContent>
          </Card>

          {/* Card: Bảng Danh sách Category Overture trong nhóm */}
          <Card>
            <CardHeader className="flex flex-col sm:flex-row sm:items-center sm:justify-between space-y-0 pb-3 gap-3">
              <div>
                <CardTitle className="text-base font-semibold">
                  Danh sách Category Overture đã liên kết ({currentGroupCategories.length})
                </CardTitle>
                <CardDescription className="text-xs">
                  Tất cả các địa điểm POI có category trong bảng này sẽ được nhận diện là nhóm &quot;{selectedGroup}&quot;.
                </CardDescription>
              </div>

              {/* Filter trong nhóm */}
              <div className="relative w-full sm:w-64">
                <Search className="absolute left-2.5 top-2.5 h-3.5 w-3.5 text-muted-foreground" />
                <Input
                  type="text"
                  placeholder="Aa"
                  value={detailSearch}
                  onChange={(e) => {
                    setDetailSearch(e.target.value);
                    setDetailPage(1);
                  }}
                  className="pl-8 h-8 text-xs font-mono"
                />
              </div>
            </CardHeader>

            <CardContent className="p-0">
              <div className="relative overflow-x-auto max-h-[calc(100vh-380px)] min-h-[300px] overflow-y-auto">
                <Table>
                  <TableHeader className="sticky top-0 z-10 bg-muted/90 backdrop-blur shadow-xs">
                    <TableRow>
                      <TableHead className="w-12 text-center">#</TableHead>
                      <TableHead>Tên Category Overture</TableHead>
                      <TableHead className="w-[100px] text-right">Thao tác</TableHead>
                    </TableRow>
                  </TableHeader>

                  <TableBody>
                    {paginatedDetailCategories.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={3} className="h-32 text-center text-xs text-muted-foreground">
                          {detailSearch
                            ? `Không tìm thấy category nào khớp với "${detailSearch}".`
                            : "Nhóm này chưa có category nào. Hãy dùng ô thêm phía trên để bổ sung category."}
                        </TableCell>
                      </TableRow>
                    ) : (
                      paginatedDetailCategories.map((cat, idx) => {
                        const globalIdx = (detailPage - 1) * detailPageSize + idx + 1;
                        return (
                          <TableRow key={cat} className="hover:bg-muted/40">
                            <TableCell className="text-center font-mono text-xs text-muted-foreground">
                              {globalIdx}
                            </TableCell>

                            <TableCell>
                              <div className="flex items-center gap-2">
                                <span className="font-mono text-sm font-medium text-foreground">
                                  {cat}
                                </span>
                              </div>
                            </TableCell>

                            <TableCell className="text-right">
                              <Button
                                size="icon"
                                variant="ghost"
                                className="h-8 w-8 text-muted-foreground hover:text-destructive"
                                title={`Xoá ${cat} khỏi nhóm`}
                                onClick={() => void handleRemoveCategory(selectedGroup, cat)}
                              >
                                <Trash2 className="h-3.5 w-3.5" />
                              </Button>
                            </TableCell>
                          </TableRow>
                        );
                      })
                    )}
                  </TableBody>
                </Table>
              </div>

              {/* Detail Pagination */}
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between border-t px-4 py-3 bg-card">
                <div className="flex flex-wrap items-center gap-4 text-xs text-muted-foreground">
                  <div>
                    Hiển thị{" "}
                    <span className="font-semibold text-foreground">
                      {filteredDetailCategories.length === 0 ? 0 : (detailPage - 1) * detailPageSize + 1}
                    </span>{" "}
                    –{" "}
                    <span className="font-semibold text-foreground">
                      {Math.min(detailPage * detailPageSize, filteredDetailCategories.length)}
                    </span>{" "}
                    trong{" "}
                    <span className="font-semibold text-foreground">
                      {filteredDetailCategories.length}
                    </span>{" "}
                    categories
                  </div>

                  <div className="flex items-center gap-1.5">
                    <span>Số dòng:</span>
                    <select
                      value={detailPageSize}
                      onChange={(e) => {
                        setDetailPageSize(Number(e.target.value));
                        setDetailPage(1);
                      }}
                      className="h-8 rounded-md border border-input bg-background px-2 py-1 text-xs shadow-xs focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring text-foreground font-medium cursor-pointer"
                    >
                      <option value={5}>5 / trang</option>
                      <option value={10}>10 / trang</option>
                      <option value={15}>15 / trang</option>
                      <option value={25}>25 / trang</option>
                      <option value={50}>50 / trang</option>
                    </select>
                  </div>
                </div>

                <div className="flex items-center gap-1">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setDetailPage(1)}
                    disabled={detailPage <= 1 || loading}
                    className="h-8 w-8 p-0 text-xs"
                    title="Trang đầu"
                  >
                    <ChevronsLeft className="h-4 w-4" />
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setDetailPage((p) => Math.max(1, p - 1))}
                    disabled={detailPage <= 1 || loading}
                    className="h-8 w-8 p-0 text-xs"
                    title="Trang trước"
                  >
                    <ChevronLeft className="h-4 w-4" />
                  </Button>

                  {getPageNumbers(detailPage, totalDetailPages).map((pNum, idx) =>
                    pNum === "..." ? (
                      <span key={`detail-ellipsis-${idx}`} className="px-1 text-xs text-muted-foreground select-none">
                        …
                      </span>
                    ) : (
                      <Button
                        key={`detail-page-${pNum}`}
                        variant={detailPage === pNum ? "default" : "outline"}
                        size="sm"
                        onClick={() => setDetailPage(pNum as number)}
                        disabled={loading}
                        className={`h-8 min-w-[32px] px-2 text-xs font-medium ${
                          detailPage === pNum ? "pointer-events-none" : ""
                        }`}
                      >
                        {pNum}
                      </Button>
                    )
                  )}

                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setDetailPage((p) => Math.min(totalDetailPages, p + 1))}
                    disabled={detailPage >= totalDetailPages || loading}
                    className="h-8 w-8 p-0 text-xs"
                    title="Trang sau"
                  >
                    <ChevronRight className="h-4 w-4" />
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setDetailPage(totalDetailPages)}
                    disabled={detailPage >= totalDetailPages || loading}
                    className="h-8 w-8 p-0 text-xs"
                    title="Trang cuối"
                  >
                    <ChevronsRight className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
      )}

      {/* ======================================================== */}
      {/* TAB 2: SPECIFIC OVERRIDES TABLE                          */}
      {/* ======================================================== */}
      {activeTab === "overrides" && (
        <div className="space-y-4">
          <Card>
            <CardContent className="pt-5">
              <div className="flex flex-col gap-3 md:flex-row md:items-center md:justify-between">
                <div className="relative flex-1 max-w-md">
                  <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
                  <Input
                    type="text"
                    placeholder="Tìm kiếm từ khoá hoặc category đích..."
                    value={searchQuery}
                    onChange={(e) => setSearchQuery(e.target.value)}
                    className="pl-9 text-sm"
                  />
                </div>

                <div className="flex items-center gap-2 text-xs text-muted-foreground">
                  <span>
                    Hiển thị <strong>{filteredOverrides.length}</strong> / {totalOverrides} quy tắc
                  </span>
                  {searchQuery && (
                    <Button
                      variant="ghost"
                      size="sm"
                      onClick={() => setSearchQuery("")}
                      className="h-8 px-2 text-xs"
                    >
                      <X className="h-3 w-3 mr-1" /> Xoá lọc
                    </Button>
                  )}
                </div>
              </div>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-3">
              <div>
                <CardTitle className="text-base font-semibold">
                  Danh sách Quy tắc Ghi đè Đặc tả (Specific Overrides)
                </CardTitle>
                <CardDescription className="text-xs">
                  Khi người dùng yêu cầu món cụ thể (ví dụ &quot;ramen&quot;, &quot;sushi&quot;, &quot;pizza&quot;), hệ thống sẽ yêu cầu chính xác category này thay vì toàn bộ nhóm chung.
                </CardDescription>
              </div>
            </CardHeader>

            <CardContent className="p-0">
              <div className="relative overflow-x-auto max-h-[calc(100vh-360px)] min-h-[340px] overflow-y-auto">
                <Table>
                  <TableHeader className="sticky top-0 z-10 bg-muted/90 backdrop-blur shadow-xs">
                    <TableRow>
                      <TableHead className="w-12 text-center">#</TableHead>
                      <TableHead className="w-[240px]">Từ khoá nhận diện (Keyword)</TableHead>
                      <TableHead>Category Overture Đích</TableHead>
                      <TableHead className="w-[200px]">Phạm vi áp dụng</TableHead>
                      <TableHead className="w-[100px] text-right">Thao tác</TableHead>
                    </TableRow>
                  </TableHeader>

                  <TableBody>
                    {loading ? (
                      <TableRow>
                        <TableCell colSpan={5} className="h-36 text-center text-sm text-muted-foreground">
                          <div className="flex items-center justify-center gap-2">
                            <Loader2 className="h-4 w-4 animate-spin text-primary" />
                            <span>Đang nạp danh sách override...</span>
                          </div>
                        </TableCell>
                      </TableRow>
                    ) : paginatedOverrides.length === 0 ? (
                      <TableRow>
                        <TableCell colSpan={5} className="h-36 text-center text-sm text-muted-foreground">
                          Chưa có quy tắc ghi đè nào phù hợp.
                        </TableCell>
                      </TableRow>
                    ) : (
                      paginatedOverrides.map(([keyword, targetCat], idx) => {
                        const globalIndex = (overridePage - 1) * overridePageSize + idx + 1;
                        return (
                          <TableRow key={keyword} className="hover:bg-muted/40">
                            <TableCell className="text-center font-mono text-xs text-muted-foreground">
                              {globalIndex}
                            </TableCell>

                            <TableCell>
                              <span className="font-semibold text-sm text-foreground">
                                {keyword}
                              </span>
                            </TableCell>

                            <TableCell>
                              <Badge variant="outline" className="font-mono text-xs text-primary border-primary/30 bg-primary/5">
                                {targetCat}
                              </Badge>
                            </TableCell>

                            <TableCell className="text-xs text-muted-foreground">
                              Đặc tả món ăn / loại quán con
                            </TableCell>

                            <TableCell className="text-right">
                              <Button
                                size="icon"
                                variant="ghost"
                                className="h-8 w-8 text-muted-foreground hover:text-destructive"
                                title="Xoá override"
                                onClick={() => handleDeleteOverride(keyword)}
                              >
                                <Trash2 className="h-3.5 w-3.5" />
                              </Button>
                            </TableCell>
                          </TableRow>
                        );
                      })
                    )}
                  </TableBody>
                </Table>
              </div>

              {/* Pagination Controls */}
              <div className="flex flex-col gap-3 sm:flex-row sm:items-center sm:justify-between border-t px-4 py-3 bg-card">
                <div className="flex flex-wrap items-center gap-4 text-xs text-muted-foreground">
                  <div>
                    Hiển thị{" "}
                    <span className="font-semibold text-foreground">
                      {filteredOverrides.length === 0 ? 0 : (overridePage - 1) * overridePageSize + 1}
                    </span>{" "}
                    –{" "}
                    <span className="font-semibold text-foreground">
                      {Math.min(overridePage * overridePageSize, filteredOverrides.length)}
                    </span>{" "}
                    trong{" "}
                    <span className="font-semibold text-foreground">
                      {filteredOverrides.length}
                    </span>{" "}
                    quy tắc
                  </div>

                  <div className="flex items-center gap-1.5">
                    <span>Số dòng:</span>
                    <select
                      value={overridePageSize}
                      onChange={(e) => {
                        setOverridePageSize(Number(e.target.value));
                        setOverridePage(1);
                      }}
                      className="h-8 rounded-md border border-input bg-background px-2 py-1 text-xs shadow-xs focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring text-foreground font-medium cursor-pointer"
                    >
                      <option value={5}>5 / trang</option>
                      <option value={10}>10 / trang</option>
                      <option value={15}>15 / trang</option>
                      <option value={25}>25 / trang</option>
                      <option value={50}>50 / trang</option>
                    </select>
                  </div>
                </div>

                <div className="flex items-center gap-1">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setOverridePage(1)}
                    disabled={overridePage <= 1 || loading}
                    className="h-8 w-8 p-0 text-xs"
                    title="Trang đầu"
                  >
                    <ChevronsLeft className="h-4 w-4" />
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setOverridePage((p) => Math.max(1, p - 1))}
                    disabled={overridePage <= 1 || loading}
                    className="h-8 w-8 p-0 text-xs"
                    title="Trang trước"
                  >
                    <ChevronLeft className="h-4 w-4" />
                  </Button>

                  {getPageNumbers(overridePage, totalOverridePages).map((pNum, idx) =>
                    pNum === "..." ? (
                      <span key={`override-ellipsis-${idx}`} className="px-1 text-xs text-muted-foreground select-none">
                        …
                      </span>
                    ) : (
                      <Button
                        key={`override-page-${pNum}`}
                        variant={overridePage === pNum ? "default" : "outline"}
                        size="sm"
                        onClick={() => setOverridePage(pNum as number)}
                        disabled={loading}
                        className={`h-8 min-w-[32px] px-2 text-xs font-medium ${
                          overridePage === pNum ? "pointer-events-none" : ""
                        }`}
                      >
                        {pNum}
                      </Button>
                    )
                  )}

                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setOverridePage((p) => Math.min(totalOverridePages, p + 1))}
                    disabled={overridePage >= totalOverridePages || loading}
                    className="h-8 w-8 p-0 text-xs"
                    title="Trang sau"
                  >
                    <ChevronRight className="h-4 w-4" />
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setOverridePage(totalOverridePages)}
                    disabled={overridePage >= totalOverridePages || loading}
                    className="h-8 w-8 p-0 text-xs"
                    title="Trang cuối"
                  >
                    <ChevronsRight className="h-4 w-4" />
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>
        </div>
      )}

      {/* ======================================================== */}
      {/* TAB 3: MATCH SIMULATOR                                   */}
      {/* ======================================================== */}
      {activeTab === "simulator" && (
        <div className="space-y-6">
          <Card>
            <CardHeader>
              <div className="flex items-center gap-2">
                <FlaskConical className="h-5 w-5 text-primary" />
                <CardTitle className="text-base font-semibold">
                  Bộ Giả lập &amp; Chẩn đoán Khớp Thể loại (Category Match Simulator)
                </CardTitle>
              </div>
              <CardDescription className="text-xs">
                Mô phỏng chính xác logic tính điểm `CategoryMatch` của backend để xác định POI sẽ được chấm bằng mô hình học máy LambdaMART hay rơi về Thuật toán hệ thống (GSKQ).
              </CardDescription>
            </CardHeader>

            <CardContent className="space-y-6">
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
                <div className="space-y-1.5">
                  <Label className="text-xs font-semibold">Category POI (Địa điểm)</Label>
                  <Input
                    value={testPlaceCategory}
                    onChange={(e) => setTestPlaceCategory(e.target.value)}
                    placeholder="ví dụ: sushi_restaurant"
                    className="font-mono text-xs"
                  />
                  <p className="text-[11px] text-muted-foreground">Category Overture thực tế của quán.</p>
                </div>

                <div className="space-y-1.5">
                  <Label className="text-xs font-semibold">Target Category (MiniMax đoán)</Label>
                  <Input
                    value={testTargetCategory}
                    onChange={(e) => setTestTargetCategory(e.target.value)}
                    placeholder="ví dụ: japanese hoặc other"
                    className="font-mono text-xs"
                  />
                  <p className="text-[11px] text-muted-foreground">1 trong 13 nhóm hoặc &quot;other&quot;.</p>
                </div>

                <div className="space-y-1.5">
                  <Label className="text-xs font-semibold">Specific Preference (Sở thích món)</Label>
                  <Input
                    value={testSpecificPreference}
                    onChange={(e) => setTestSpecificPreference(e.target.value)}
                    placeholder="ví dụ: sushi, ramen (hoặc để trống)"
                    className="text-xs"
                  />
                  <p className="text-[11px] text-muted-foreground">Từ khoá đặc tả nếu người dùng yêu cầu.</p>
                </div>

                <div className="space-y-1.5">
                  <Label className="text-xs font-semibold">Raw Query (Câu hỏi người dùng)</Label>
                  <Input
                    value={testRawQuery}
                    onChange={(e) => setTestRawQuery(e.target.value)}
                    placeholder="ví dụ: tìm quán sushi ngon"
                    className="text-xs"
                  />
                  <p className="text-[11px] text-muted-foreground">Truy vấn văn bản ban đầu.</p>
                </div>
              </div>

              <div className="flex items-center justify-between pt-4 border-t">
                <Button
                  size="sm"
                  onClick={handleRunTestMatch}
                  disabled={isTesting}
                  className="gap-2"
                >
                  {isTesting ? <Loader2 className="h-4 w-4 animate-spin" /> : <Zap className="h-4 w-4" />}
                  Chạy Thử nghiệm Tính Điểm
                </Button>

                {testResult && (
                  <div className="flex items-center gap-4">
                    <div className="flex items-center gap-1.5">
                      <span className="text-xs text-muted-foreground">Điểm CategoryMatch:</span>
                      <Badge
                        variant={testResult.categoryMatchScore > 0 ? "default" : "destructive"}
                        className="font-mono text-xs"
                      >
                        {testResult.categoryMatchScore.toFixed(1)}
                      </Badge>
                    </div>

                    <div className="flex items-center gap-1.5">
                      <span className="text-xs text-muted-foreground">Luồng xếp hạng:</span>
                      {testResult.categoryMatchScore > 0 ? (
                        <Badge variant="outline" className="border-emerald-500/50 text-emerald-600 bg-emerald-500/10 text-xs gap-1">
                          <Bot className="h-3 w-3" />
                          Mô hình AI (LambdaMART ONNX)
                        </Badge>
                      ) : (
                        <Badge variant="outline" className="border-amber-500/50 text-amber-600 bg-amber-500/10 text-xs gap-1">
                          <ArrowRight className="h-3 w-3" />
                          Thuật toán hệ thống (GSKQ)
                        </Badge>
                      )}
                    </div>
                  </div>
                )}
              </div>
            </CardContent>
          </Card>
        </div>
      )}

      {/* DIALOG: ĐỔI TÊN NHÓM */}
      <Dialog open={isRenameOpen} onOpenChange={setIsRenameOpen}>
        <DialogContent className="sm:max-w-[420px]">
          <form onSubmit={handleRenameGroup}>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <Pencil className="h-4 w-4 text-primary" />
                Đổi Tên Nhóm Thể Loại
              </DialogTitle>
              <DialogDescription className="text-xs">
                Đổi tên định danh nhóm từ <code className="font-mono font-bold text-primary">{selectedGroup}</code> sang tên mới.
              </DialogDescription>
            </DialogHeader>

            <div className="space-y-3 py-4">
              <div className="space-y-1.5">
                <Label htmlFor="renameInput" className="text-xs font-semibold">Tên Nhóm Mới (Group Key)</Label>
                <Input
                  id="renameInput"
                  value={renameNewName}
                  onChange={(e) => setRenameNewName(e.target.value)}
                  className="font-mono text-xs"
                  placeholder="ví dụ: cafe, coffee_tea, japanese..."
                />
                <p className="text-[11px] text-muted-foreground">
                  Dùng chữ thường không dấu, dạng snake_case.
                </p>
              </div>
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" size="sm" onClick={() => setIsRenameOpen(false)}>
                Huỷ
              </Button>
              <Button type="submit" size="sm" disabled={isRenaming}>
                {isRenaming && <Loader2 className="h-3.5 w-3.5 mr-1.5 animate-spin" />}
                Lưu Tên Mới
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* DIALOG: THÊM NHÓM MỚI */}
      <Dialog open={isAddGroupOpen} onOpenChange={setIsAddGroupOpen}>
        <DialogContent className="sm:max-w-[480px]">
          <form onSubmit={handleAddGroup}>
            <DialogHeader>
              <DialogTitle>Thêm Nhóm Thể Loại Mới</DialogTitle>
              <DialogDescription className="text-xs">
                Tạo một nhóm phân loại mới cho hệ thống tìm kiếm và mô hình xếp hạng.
              </DialogDescription>
            </DialogHeader>

            <div className="space-y-4 py-4">
              <div className="space-y-1.5">
                <Label htmlFor="groupName" className="text-xs font-semibold">
                  Mã Nhóm (Group Key)
                </Label>
                <Input
                  id="groupName"
                  placeholder="ví dụ: cinema, museum, bakery..."
                  value={newGroupName}
                  onChange={(e) => setNewGroupName(e.target.value)}
                  className="font-mono text-xs"
                />
                <p className="text-[11px] text-muted-foreground">
                  Dùng chữ thường không dấu, snake_case (ví dụ: `bbq_hotpot`, `gym_fitness`).
                </p>
              </div>

              <div className="space-y-1.5">
                <Label htmlFor="initialCats" className="text-xs font-semibold">
                  Categories Overture Ban Đầu
                </Label>
                <Input
                  id="initialCats"
                  placeholder="movie_theater, cinema, drive_in_theater"
                  value={newGroupCategories}
                  onChange={(e) => setNewGroupCategories(e.target.value)}
                  className="font-mono text-xs"
                />
                <p className="text-[11px] text-muted-foreground">
                  Phân cách nhau bằng dấu phẩy hoặc khoảng trắng.
                </p>
              </div>
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" size="sm" onClick={() => setIsAddGroupOpen(false)}>
                Huỷ
              </Button>
              <Button type="submit" size="sm" disabled={isSubmittingGroup}>
                {isSubmittingGroup && <Loader2 className="h-3.5 w-3.5 mr-1.5 animate-spin" />}
                Lưu Nhóm Mới
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* DIALOG: THÊM OVERRIDE */}
      <Dialog open={isAddOverrideOpen} onOpenChange={setIsAddOverrideOpen}>
        <DialogContent className="sm:max-w-[480px]">
          <form onSubmit={handleAddOverride}>
            <DialogHeader>
              <DialogTitle>Thêm Quy tắc Đặc tả (Override)</DialogTitle>
              <DialogDescription className="text-xs">
                Ưu tiên yêu cầu category chuẩn khi người dùng tìm kiếm từ khoá món cụ thể.
              </DialogDescription>
            </DialogHeader>

            <div className="space-y-4 py-4">
              <div className="space-y-1.5">
                <Label className="text-xs font-semibold">Từ khoá nhận diện (Keyword)</Label>
                <Input
                  placeholder="ví dụ: ramen, bánh xèo, steak, trà sữa..."
                  value={newOverrideKeyword}
                  onChange={(e) => setNewOverrideKeyword(e.target.value)}
                  className="text-xs"
                />
                <p className="text-[11px] text-muted-foreground">
                  Từ hoặc cụm từ người dùng thường nhập.
                </p>
              </div>

              <div className="space-y-1.5">
                <Label className="text-xs font-semibold">Overture Category Mục Tiêu</Label>
                <Input
                  placeholder="ví dụ: ramen_restaurant, bubble_tea_shop..."
                  value={newOverrideTarget}
                  onChange={(e) => setNewOverrideTarget(e.target.value)}
                  className="font-mono text-xs"
                />
                <p className="text-[11px] text-muted-foreground">
                  Tên category chuẩn trong dữ liệu Overture của địa điểm.
                </p>
              </div>
            </div>

            <DialogFooter>
              <Button type="button" variant="outline" size="sm" onClick={() => setIsAddOverrideOpen(false)}>
                Huỷ
              </Button>
              <Button type="submit" size="sm" disabled={isSubmittingOverride}>
                {isSubmittingOverride && <Loader2 className="h-3.5 w-3.5 mr-1.5 animate-spin" />}
                Lưu Quy Tắc
              </Button>
            </DialogFooter>
          </form>
        </DialogContent>
      </Dialog>

      {/* DIALOG: RESET CONFIRM */}
      <Dialog open={isResetConfirmOpen} onOpenChange={setIsResetConfirmOpen}>
        <DialogContent className="sm:max-w-[420px]">
          <DialogHeader>
            <DialogTitle className="text-amber-600 flex items-center gap-2">
              <RotateCcw className="h-5 w-5" />
              Khôi phục 13 nhóm chuẩn Colab?
            </DialogTitle>
            <DialogDescription className="text-xs pt-2">
              Thao tác này sẽ ghi đè toàn bộ cấu hình về 13 nhóm và 31 categories chuẩn của mô hình học máy:
              `cafe`, `viet`, `japanese`, `korean`, `thai`, `seafood`, `western`, `vegetarian`, `chinese`, `bbq_hotpot`, `bookstore`, `library`, `park`.
            </DialogDescription>
          </DialogHeader>

          <DialogFooter className="pt-4">
            <Button type="button" variant="outline" size="sm" onClick={() => setIsResetConfirmOpen(false)}>
              Huỷ
            </Button>
            <Button
              type="button"
              variant="destructive"
              size="sm"
              disabled={isResetting}
              onClick={handleResetToDefaults}
              className="bg-amber-600 hover:bg-amber-700"
            >
              {isResetting && <Loader2 className="h-3.5 w-3.5 mr-1.5 animate-spin" />}
              Xác nhận khôi phục
            </Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
