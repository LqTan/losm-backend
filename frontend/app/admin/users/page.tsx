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
import type { AdminUser, PagedResult } from "@/lib/types";
import {
  ChevronLeft,
  ChevronRight,
  ChevronsLeft,
  ChevronsRight,
  Loader2,
  Pencil,
  RefreshCw,
  Search,
  Shield,
  Trash2,
  User,
  UserPlus,
  Users,
  X,
} from "lucide-react";
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

export default function UsersAdminPage() {
  const [users, setUsers] = useState<AdminUser[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [page, setPage] = useState(1);
  const [pageSize, setPageSize] = useState(15);
  const [totalPages, setTotalPages] = useState(1);
  const [loading, setLoading] = useState(true);

  // Search
  const [search, setSearch] = useState("");
  const [searchInput, setSearchInput] = useState("");

  // Checkbox selection state
  const [selectedIds, setSelectedIds] = useState<string[]>([]);

  // Modals
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<AdminUser | null>(null);
  const [deleting, setDeleting] = useState<AdminUser | null>(null);
  const [deletingLoading, setDeletingLoading] = useState(false);
  const [batchDeleting, setBatchDeleting] = useState(false);
  const [batchDeletingLoading, setBatchDeletingLoading] = useState(false);

  const loadUsers = useCallback(
    async (
      targetPage: number = page,
      targetSearch: string = search,
      targetPageSize: number = pageSize,
    ) => {
      setLoading(true);
      try {
        const token = getToken();
        const params = new URLSearchParams({
          page: targetPage.toString(),
          pageSize: targetPageSize.toString(),
        });
        if (targetSearch.trim()) params.set("query", targetSearch.trim());

        const res = await api.get<PagedResult<AdminUser>>(
          `/api/admin/users?${params.toString()}`,
          token,
        );

        setUsers(res.items);
        setTotalCount(res.totalCount);
        setPage(res.page);
        setTotalPages(Math.max(1, res.totalPages));
        // Reset selection if list changed
        setSelectedIds((prev) =>
          prev.filter((id) => res.items.some((u) => u.id === id)),
        );
      } catch (err) {
        toast.error(
          err instanceof ApiError
            ? err.message
            : "Không thể tải danh sách người dùng",
        );
      } finally {
        setLoading(false);
      }
    },
    [page, search, pageSize],
  );

  useEffect(() => {
    void loadUsers(page, search, pageSize);
  }, [page, search, pageSize, loadUsers]);

  function handleSearchSubmit(e: React.FormEvent) {
    e.preventDefault();
    setPage(1);
    setSearch(searchInput);
  }

  // Header select all toggle
  const allCurrentSelected =
    users.length > 0 && users.every((u) => selectedIds.includes(u.id));

  function handleSelectAll() {
    if (allCurrentSelected) {
      // Unselect all current page
      const currentPageIds = new Set(users.map((u) => u.id));
      setSelectedIds((prev) => prev.filter((id) => !currentPageIds.has(id)));
    } else {
      // Select all current page
      const newIds = new Set([...selectedIds, ...users.map((u) => u.id)]);
      setSelectedIds(Array.from(newIds));
    }
  }

  function handleToggleSelect(id: string) {
    setSelectedIds((prev) =>
      prev.includes(id) ? prev.filter((item) => item !== id) : [...prev, id],
    );
  }

  async function handleDeleteConfirm() {
    if (!deleting) return;
    setDeletingLoading(true);
    try {
      const token = getToken();
      await api.del(`/api/admin/users/${deleting.id}`, token);
      toast.success(`Đã xóa người dùng "${deleting.username}"`);
      setSelectedIds((prev) => prev.filter((id) => id !== deleting.id));
      setDeleting(null);
      void loadUsers();
    } catch (err) {
      toast.error(
        err instanceof ApiError ? err.message : "Xóa người dùng thất bại",
      );
    } finally {
      setDeletingLoading(false);
    }
  }

  async function handleBatchDeleteConfirm() {
    if (selectedIds.length === 0) return;
    setBatchDeletingLoading(true);
    try {
      const token = getToken();
      await api.post(
        "/api/admin/users/delete-batch",
        { ids: selectedIds },
        token,
      );
      toast.success(`Đã xóa thành công ${selectedIds.length} người dùng`);
      setSelectedIds([]);
      setBatchDeleting(false);
      void loadUsers();
    } catch (err) {
      toast.error(
        err instanceof ApiError ? err.message : "Xóa hàng loạt thất bại",
      );
    } finally {
      setBatchDeletingLoading(false);
    }
  }

  return (
    <div className="space-y-6">
      {/* Top Header */}
      <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
        <div>
          <h2 className="text-2xl font-bold tracking-tight">Quản lý Người dùng</h2>
          <p className="text-sm text-muted-foreground">
            Xem danh sách tài khoản, phân quyền quản trị và quản lý người dùng hệ thống.
          </p>
        </div>
        <div className="flex items-center gap-2">
          {selectedIds.length > 0 && (
            <Button
              variant="destructive"
              size="sm"
              onClick={() => setBatchDeleting(true)}
              className="gap-1.5"
            >
              <Trash2 className="h-3.5 w-3.5" />
              <span>Xóa đã chọn ({selectedIds.length})</span>
            </Button>
          )}

          <Button
            variant="outline"
            size="sm"
            onClick={() => void loadUsers()}
            disabled={loading}
          >
            <RefreshCw
              className={`mr-1.5 h-3.5 w-3.5 ${loading ? "animate-spin" : ""}`}
            />
            Làm mới
          </Button>

          <Button
            size="sm"
            onClick={() => setCreating(true)}
            className="gap-1.5"
          >
            <UserPlus className="h-3.5 w-3.5" />
            <span>Thêm người dùng</span>
          </Button>
        </div>
      </div>

      {/* Filter and Search Bar */}
      <Card>
        <CardContent className="pt-5">
          <form
            onSubmit={handleSearchSubmit}
            className="flex flex-col gap-3 sm:flex-row sm:items-center"
          >
            <div className="relative flex-1">
              <Search className="absolute left-2.5 top-2.5 h-4 w-4 text-muted-foreground" />
              <Input
                type="text"
                placeholder="Tìm theo username hoặc email..."
                value={searchInput}
                onChange={(e) => setSearchInput(e.target.value)}
                className="pl-9 text-sm"
              />
            </div>
            <div className="flex items-center gap-2">
              <Button type="submit" size="sm" variant="secondary">
                Tìm
              </Button>
              {search && (
                <Button
                  type="button"
                  size="sm"
                  variant="ghost"
                  onClick={() => {
                    setSearchInput("");
                    setSearch("");
                    setPage(1);
                  }}
                >
                  <X className="mr-1 h-3.5 w-3.5" />
                  Xóa lọc
                </Button>
              )}
            </div>
          </form>
        </CardContent>
      </Card>

      {/* Users Table */}
      <Card>
        <CardHeader className="flex flex-row items-center justify-between pb-3">
          <div>
            <CardTitle className="text-base font-semibold">
              Danh sách tài khoản
            </CardTitle>
            <CardDescription className="text-xs">
              Tổng cộng {totalCount.toLocaleString()} người dùng
              {selectedIds.length > 0 && ` (Đã chọn ${selectedIds.length})`}
            </CardDescription>
          </div>
        </CardHeader>
        <CardContent className="p-0">
          <div className="overflow-x-auto">
            <Table>
              <TableHeader>
                <TableRow>
                  {/* Bare checkbox without any text label */}
                  <TableHead className="w-12 text-center">
                    <input
                      type="checkbox"
                      aria-label="Chọn tất cả"
                      checked={allCurrentSelected}
                      onChange={handleSelectAll}
                      className="h-4 w-4 rounded border-border text-primary accent-primary cursor-pointer align-middle"
                    />
                  </TableHead>
                  <TableHead className="min-w-[180px]">Người dùng</TableHead>
                  <TableHead className="min-w-[200px]">Email</TableHead>
                  <TableHead className="min-w-[100px]">Vai trò</TableHead>
                  <TableHead className="min-w-[140px]">Ngày tạo</TableHead>
                  <TableHead className="w-[100px] text-right">Thao tác</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {loading && users.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6} className="h-32 text-center">
                      <div className="flex flex-col items-center justify-center gap-2 text-muted-foreground">
                        <Loader2 className="h-6 w-6 animate-spin text-primary" />
                        <span className="text-xs">Đang tải người dùng...</span>
                      </div>
                    </TableCell>
                  </TableRow>
                ) : users.length === 0 ? (
                  <TableRow>
                    <TableCell colSpan={6} className="h-32 text-center">
                      <div className="flex flex-col items-center justify-center gap-2 text-muted-foreground">
                        <Users className="h-8 w-8 stroke-[1.5]" />
                        <span className="text-sm font-medium">
                          Không tìm thấy người dùng nào
                        </span>
                      </div>
                    </TableCell>
                  </TableRow>
                ) : (
                  users.map((user) => {
                    const isSelected = selectedIds.includes(user.id);
                    const isAdmin = user.role?.toLowerCase() === "admin";

                    return (
                      <TableRow
                        key={user.id}
                        className={`transition-colors hover:bg-muted/40 ${
                          isSelected ? "bg-muted/60" : ""
                        }`}
                      >
                        {/* Row Checkbox */}
                        <TableCell className="w-12 text-center">
                          <input
                            type="checkbox"
                            aria-label={`Chọn ${user.username}`}
                            checked={isSelected}
                            onChange={() => handleToggleSelect(user.id)}
                            className="h-4 w-4 rounded border-border text-primary accent-primary cursor-pointer align-middle"
                          />
                        </TableCell>

                        {/* Username & Avatar */}
                        <TableCell>
                          <div className="flex items-center gap-2.5">
                            <div className="flex h-8 w-8 items-center justify-center rounded-full bg-primary/10 text-primary font-semibold text-xs shrink-0">
                              {user.username.slice(0, 2).toUpperCase()}
                            </div>
                            <div className="min-w-0">
                              <div className="font-medium text-sm truncate">
                                {user.username}
                              </div>
                              <div className="font-mono text-[10px] text-muted-foreground truncate">
                                {user.id}
                              </div>
                            </div>
                          </div>
                        </TableCell>

                        {/* Email */}
                        <TableCell className="text-sm text-muted-foreground">
                          {user.email}
                        </TableCell>

                        {/* Role */}
                        <TableCell>
                          <Badge
                            variant={isAdmin ? "default" : "secondary"}
                            className="text-xs font-normal capitalize"
                          >
                            {isAdmin ? (
                              <Shield className="mr-1 h-3 w-3 inline" />
                            ) : (
                              <User className="mr-1 h-3 w-3 inline" />
                            )}
                            {user.role}
                          </Badge>
                        </TableCell>

                        {/* Created At */}
                        <TableCell className="text-xs text-muted-foreground whitespace-nowrap">
                          {new Date(user.createdAt).toLocaleDateString("vi-VN", {
                            year: "numeric",
                            month: "short",
                            day: "numeric",
                            hour: "2-digit",
                            minute: "2-digit",
                          })}
                        </TableCell>

                        {/* Actions */}
                        <TableCell className="text-right">
                          <div className="flex items-center justify-end gap-1">
                            <Button
                              variant="ghost"
                              size="icon"
                              className="h-8 w-8 text-muted-foreground hover:text-foreground"
                              title="Chỉnh sửa người dùng"
                              onClick={() => setEditing(user)}
                            >
                              <Pencil className="h-4 w-4" />
                            </Button>
                            <Button
                              variant="ghost"
                              size="icon"
                              className="h-8 w-8 text-destructive hover:bg-destructive/10 hover:text-destructive"
                              title="Xóa người dùng"
                              onClick={() => setDeleting(user)}
                            >
                              <Trash2 className="h-4 w-4" />
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
          <div className="flex flex-col gap-3 border-t p-4 sm:flex-row sm:items-center sm:justify-between">
            <div className="flex items-center gap-3 text-xs text-muted-foreground">
              <span>
                Trang {page} / {totalPages} ({totalCount.toLocaleString()} người dùng)
              </span>
              <div className="flex items-center gap-1.5">
                <span>Số hàng:</span>
                <select
                  value={pageSize}
                  onChange={(e) => {
                    setPageSize(Number(e.target.value));
                    setPage(1);
                  }}
                  className="h-7 rounded border border-border bg-background px-1.5 text-xs text-foreground"
                >
                  <option value={10}>10 / trang</option>
                  <option value={15}>15 / trang</option>
                  <option value={25}>25 / trang</option>
                  <option value={50}>50 / trang</option>
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

      {/* Create User Modal */}
      {creating && (
        <CreateUserModal
          onClose={() => setCreating(false)}
          onCreated={() => {
            setCreating(false);
            void loadUsers();
          }}
        />
      )}

      {/* Edit User Modal */}
      {editing && (
        <EditUserModal
          user={editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            void loadUsers();
          }}
        />
      )}

      {/* Single Delete Confirmation Modal */}
      {deleting && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs">
          <Card className="w-full max-w-md rounded-xl border bg-card text-card-foreground shadow-xl">
            <CardHeader className="border-b px-5 py-4">
              <CardTitle className="text-base font-semibold text-destructive">
                Xác nhận xóa người dùng
              </CardTitle>
              <CardDescription className="text-xs">
                Hành động này sẽ xóa vĩnh viễn tài khoản khỏi cơ sở dữ liệu.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-3 px-5 py-4 text-xs">
              <div className="rounded-lg border bg-muted/40 p-3 space-y-1.5">
                <div>
                  <span className="text-muted-foreground">Tài khoản: </span>
                  <span className="font-semibold text-foreground">
                    {deleting.username}
                  </span>
                </div>
                <div>
                  <span className="text-muted-foreground">Email: </span>
                  <span className="font-mono">{deleting.email}</span>
                </div>
                <div>
                  <span className="text-muted-foreground">Vai trò: </span>
                  <span>{deleting.role}</span>
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

      {/* Batch Delete Confirmation Modal */}
      {batchDeleting && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs">
          <Card className="w-full max-w-md rounded-xl border bg-card text-card-foreground shadow-xl">
            <CardHeader className="border-b px-5 py-4">
              <CardTitle className="text-base font-semibold text-destructive">
                Xác nhận xóa nhiều người dùng
              </CardTitle>
              <CardDescription className="text-xs">
                Bạn có chắc chắn muốn xóa {selectedIds.length} người dùng đã chọn không? Hành động này không thể hoàn tác.
              </CardDescription>
            </CardHeader>
            <CardContent className="space-y-4 px-5 py-4 text-xs">
              <p className="text-muted-foreground">
                Các tài khoản được chọn sẽ bị gỡ bỏ vĩnh viễn khỏi hệ thống (ngoại trừ tài khoản của chính bạn).
              </p>
              <div className="flex justify-end gap-2 pt-2">
                <Button
                  variant="outline"
                  size="sm"
                  onClick={() => setBatchDeleting(false)}
                  disabled={batchDeletingLoading}
                >
                  Hủy
                </Button>
                <Button
                  variant="destructive"
                  size="sm"
                  onClick={handleBatchDeleteConfirm}
                  disabled={batchDeletingLoading}
                >
                  {batchDeletingLoading && (
                    <Loader2 className="mr-1.5 h-3.5 w-3.5 animate-spin" />
                  )}
                  Xóa {selectedIds.length} tài khoản
                </Button>
              </div>
            </CardContent>
          </Card>
        </div>
      )}
    </div>
  );
}

// Modal tạo người dùng
function CreateUserModal({
  onClose,
  onCreated,
}: {
  onClose: () => void;
  onCreated: () => void;
}) {
  const [username, setUsername] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [role, setRole] = useState("User");
  const [saving, setSaving] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!username.trim() || !email.trim() || !password) {
      toast.error("Vui lòng điền đầy đủ thông tin");
      return;
    }

    setSaving(true);
    try {
      const token = getToken();
      await api.post(
        "/api/admin/users",
        {
          username: username.trim(),
          email: email.trim(),
          password,
          role,
        },
        token,
      );
      toast.success("Tạo người dùng thành công");
      onCreated();
    } catch (err) {
      toast.error(
        err instanceof ApiError ? err.message : "Tạo người dùng thất bại",
      );
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs">
      <Card className="w-full max-w-lg rounded-xl border bg-card text-card-foreground shadow-2xl">
        <CardHeader className="flex flex-row items-center justify-between border-b px-5 py-4">
          <div>
            <CardTitle className="text-base font-semibold">
              Thêm người dùng mới
            </CardTitle>
            <CardDescription className="text-xs">
              Tạo tài khoản người dùng hoặc quản trị viên trong hệ thống
            </CardDescription>
          </div>
          <Button
            variant="ghost"
            size="icon"
            className="h-8 w-8 text-muted-foreground"
            onClick={onClose}
          >
            <X className="h-4 w-4" />
          </Button>
        </CardHeader>
        <CardContent className="px-5 py-4">
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-1.5">
              <Label className="text-xs font-medium">Tên đăng nhập (Username)</Label>
              <Input
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                placeholder="Ví dụ: nguyenvan_a"
                required
                className="text-sm"
              />
            </div>

            <div className="space-y-1.5">
              <Label className="text-xs font-medium">Địa chỉ Email</Label>
              <Input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                placeholder="example@domain.com"
                required
                className="text-sm"
              />
            </div>

            <div className="space-y-1.5">
              <Label className="text-xs font-medium">Mật khẩu</Label>
              <Input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Tối thiểu 6 ký tự"
                required
                className="text-sm"
              />
            </div>

            <div className="space-y-1.5">
              <Label className="text-xs font-medium">Vai trò (Role)</Label>
              <select
                value={role}
                onChange={(e) => setRole(e.target.value)}
                className="h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs transition-colors focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="User">Người dùng thông thường (User)</option>
                <option value="Admin">Quản trị viên (Admin)</option>
              </select>
            </div>

            <div className="flex justify-end gap-2 pt-3 border-t">
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
                Tạo người dùng
              </Button>
            </div>
          </form>
        </CardContent>
      </Card>
    </div>
  );
}

// Modal chỉnh sửa người dùng
function EditUserModal({
  user,
  onClose,
  onSaved,
}: {
  user: AdminUser;
  onClose: () => void;
  onSaved: () => void;
}) {
  const [username, setUsername] = useState(user.username);
  const [email, setEmail] = useState(user.email);
  const [password, setPassword] = useState("");
  const [role, setRole] = useState(user.role);
  const [saving, setSaving] = useState(false);

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (!username.trim() || !email.trim()) {
      toast.error("Vui lòng điền tên đăng nhập và email");
      return;
    }

    setSaving(true);
    try {
      const token = getToken();
      await api.put(
        `/api/admin/users/${user.id}`,
        {
          username: username.trim(),
          email: email.trim(),
          role,
          password: password.trim() ? password : null,
        },
        token,
      );
      toast.success("Cập nhật người dùng thành công");
      onSaved();
    } catch (err) {
      toast.error(
        err instanceof ApiError ? err.message : "Cập nhật thất bại",
      );
    } finally {
      setSaving(false);
    }
  }

  return (
    <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 p-4 backdrop-blur-xs">
      <Card className="w-full max-w-lg rounded-xl border bg-card text-card-foreground shadow-2xl">
        <CardHeader className="flex flex-row items-center justify-between border-b px-5 py-4">
          <div>
            <CardTitle className="text-base font-semibold">
              Chỉnh sửa người dùng
            </CardTitle>
            <CardDescription className="text-xs">
              ID: <span className="font-mono">{user.id}</span>
            </CardDescription>
          </div>
          <Button
            variant="ghost"
            size="icon"
            className="h-8 w-8 text-muted-foreground"
            onClick={onClose}
          >
            <X className="h-4 w-4" />
          </Button>
        </CardHeader>
        <CardContent className="px-5 py-4">
          <form onSubmit={handleSubmit} className="space-y-4">
            <div className="space-y-1.5">
              <Label className="text-xs font-medium">Tên đăng nhập (Username)</Label>
              <Input
                value={username}
                onChange={(e) => setUsername(e.target.value)}
                required
                className="text-sm"
              />
            </div>

            <div className="space-y-1.5">
              <Label className="text-xs font-medium">Địa chỉ Email</Label>
              <Input
                type="email"
                value={email}
                onChange={(e) => setEmail(e.target.value)}
                required
                className="text-sm"
              />
            </div>

            <div className="space-y-1.5">
              <Label className="text-xs font-medium">
                Mật khẩu mới (Để trống nếu không đổi)
              </Label>
              <Input
                type="password"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Nhập mật khẩu mới nếu muốn đặt lại..."
                className="text-sm"
              />
            </div>

            <div className="space-y-1.5">
              <Label className="text-xs font-medium">Vai trò (Role)</Label>
              <select
                value={role}
                onChange={(e) => setRole(e.target.value)}
                className="h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-xs transition-colors focus-visible:outline-hidden focus-visible:ring-1 focus-visible:ring-ring"
              >
                <option value="User">Người dùng thông thường (User)</option>
                <option value="Admin">Quản trị viên (Admin)</option>
              </select>
            </div>

            <div className="flex justify-end gap-2 pt-3 border-t">
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
        </CardContent>
      </Card>
    </div>
  );
}
