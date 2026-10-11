"use client";

import { useEffect, useState } from "react";
import { getToken } from "@/lib/auth";
import {
  Users,
  UserPlus,
  Mail,
  Check,
  X,
  Trash2,
  Clock,
  Send,
  Loader2,
  UserCheck,
  Search,
  Sparkles,
} from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { toast } from "sonner";

interface FriendItem {
  id: string; // friendship id
  friendId?: string;
  username?: string;
  email?: string;
  avatarUrl?: string;
  createdAt?: string;
  since?: string;
  friend?: {
    id?: string;
    username?: string;
    email?: string;
    avatarUrl?: string;
  };
}

interface FriendRequestItem {
  id: string;
  senderId?: string;
  senderName?: string;
  senderUsername?: string;
  senderEmail?: string;
  senderAvatarUrl?: string;
  receiverEmail?: string;
  createdAt?: string;
}

function getFriendName(item: FriendItem): string {
  return (
    item.friend?.username ||
    item.username ||
    item.friend?.email?.split("@")[0] ||
    item.email?.split("@")[0] ||
    "Bạn bè"
  );
}

function getFriendEmail(item: FriendItem): string {
  return item.friend?.email || item.email || "";
}

function getFriendAvatar(item: FriendItem): string | undefined {
  return item.friend?.avatarUrl || item.avatarUrl;
}

function getSenderName(req: FriendRequestItem): string {
  return (
    req.senderUsername ||
    req.senderName ||
    req.senderEmail?.split("@")[0] ||
    "Người dùng"
  );
}

export default function ProfileFriendsPage() {
  const [friends, setFriends] = useState<FriendItem[]>([]);
  const [incomingRequests, setIncomingRequests] = useState<FriendRequestItem[]>([]);
  const [outgoingRequests, setOutgoingRequests] = useState<FriendRequestItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);

  // Invite modal/form state
  const [showInviteModal, setShowInviteModal] = useState(false);
  const [inviteEmail, setInviteEmail] = useState("");
  const [inviteMessage, setInviteMessage] = useState("");
  const [isSendingInvite, setIsSendingInvite] = useState(false);

  // Search filter
  const [searchQuery, setSearchQuery] = useState("");
  const [activeTab, setActiveTab] = useState<"friends" | "incoming" | "outgoing">("friends");

  const [processingId, setProcessingId] = useState<string | null>(null);

  const fetchAllData = async () => {
    const token = getToken();
    if (!token) return;

    try {
      setIsLoading(true);
      const [friendsRes, requestsRes] = await Promise.all([
        fetch("/api/friends", {
          headers: { Authorization: `Bearer ${token}` },
        }),
        fetch("/api/friends/requests", {
          headers: { Authorization: `Bearer ${token}` },
        }),
      ]);

      if (friendsRes.ok) {
        const friendsData = await friendsRes.json();
        setFriends(Array.isArray(friendsData) ? friendsData : []);
      }

      if (requestsRes.ok) {
        const requestsData = await requestsRes.json();
        setIncomingRequests(Array.isArray(requestsData?.incoming) ? requestsData.incoming : []);
        setOutgoingRequests(Array.isArray(requestsData?.outgoing) ? requestsData.outgoing : []);
      }
    } catch (err) {
      console.error("Failed to fetch friends data:", err);
      toast.error("Không thể tải danh sách bạn bè");
    } finally {
      setIsLoading(false);
    }
  };

  useEffect(() => {
    fetchAllData();
  }, []);

  const handleSendInvite = async (e: React.FormEvent) => {
    e.preventDefault();
    const token = getToken();
    if (!token) return;

    const email = inviteEmail.trim().toLowerCase();
    if (!email || !email.includes("@")) {
      toast.error("Vui lòng nhập địa chỉ email hợp lệ");
      return;
    }

    try {
      setIsSendingInvite(true);
      const res = await fetch("/api/friends/invite", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          email,
          receiverEmail: email,
          message: inviteMessage.trim() || undefined,
        }),
      });

      const data = await res.json();
      if (!res.ok) {
        throw new Error(data.message || "Gửi lời mời thất bại");
      }

      toast.success(data.message || `Đã gửi lời mời kết bạn tới ${email}!`);
      setInviteEmail("");
      setInviteMessage("");
      setShowInviteModal(false);
      fetchAllData();
    } catch (err: any) {
      toast.error(err.message || "Lỗi khi gửi lời mời kết bạn");
    } finally {
      setIsSendingInvite(false);
    }
  };

  const handleRespondRequest = async (requestId: string, action: "accept" | "decline") => {
    const token = getToken();
    if (!token) return;

    try {
      setProcessingId(requestId);
      const res = await fetch("/api/friends/respond", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          Authorization: `Bearer ${token}`,
        },
        body: JSON.stringify({
          friendshipId: requestId,
          action,
          accept: action === "accept",
        }),
      });

      const data = await res.json();
      if (!res.ok) {
        throw new Error(data.message || "Xử lý thất bại");
      }

      toast.success(action === "accept" ? "Đã đồng ý kết bạn!" : "Đã từ chối lời mời");
      fetchAllData();
    } catch (err: any) {
      toast.error(err.message || "Lỗi khi xử lý lời mời");
    } finally {
      setProcessingId(null);
    }
  };

  const handleRemoveFriend = async (id: string, name: string) => {
    if (!confirm(`Bạn có chắc muốn thực hiện thao tác này với ${name}?`)) return;

    const token = getToken();
    if (!token) return;

    try {
      setProcessingId(id);
      const res = await fetch(`/api/friends/${id}`, {
        method: "DELETE",
        headers: {
          Authorization: `Bearer ${token}`,
        },
      });

      if (!res.ok) {
        const data = await res.json();
        throw new Error(data.message || "Xóa thất bại");
      }

      toast.success(`Đã hủy thành công.`);
      fetchAllData();
    } catch (err: any) {
      toast.error(err.message || "Lỗi khi xử lý");
    } finally {
      setProcessingId(null);
    }
  };

  const filteredFriends = friends.filter((item) => {
    const q = searchQuery.toLowerCase();
    const name = getFriendName(item).toLowerCase();
    const email = getFriendEmail(item).toLowerCase();
    return name.includes(q) || email.includes(q);
  });

  return (
    <div className="space-y-6">
      {/* Header section */}
      <div className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 bg-card p-6 rounded-2xl border border-border shadow-sm">
        <div>
          <h2 className="text-xl font-bold tracking-tight text-foreground flex items-center gap-2">
            <Users className="h-6 w-6 text-primary" />
            Bạn bè & Kết nối
          </h2>
          <p className="text-sm text-muted-foreground mt-1">
            Quản lý bạn bè và kết nối để cùng lên lịch hẹn, tìm điểm hẹn chung công bằng.
          </p>
        </div>
        <Button
          onClick={() => setShowInviteModal(true)}
          className="gap-2 bg-primary text-primary-foreground hover:bg-primary/90 shadow-sm"
        >
          <UserPlus className="h-4 w-4" />
          <span>Thêm bạn bè</span>
        </Button>
      </div>

      {/* Tabs navigation */}
      <div className="flex items-center gap-2 border-b border-border pb-2">
        <button
          onClick={() => setActiveTab("friends")}
          className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
            activeTab === "friends"
              ? "bg-primary/10 text-primary"
              : "text-muted-foreground hover:bg-muted"
          }`}
        >
          <UserCheck className="h-4 w-4" />
          <span>Bạn bè ({friends.length})</span>
        </button>

        <button
          onClick={() => setActiveTab("incoming")}
          className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-colors relative ${
            activeTab === "incoming"
              ? "bg-primary/10 text-primary"
              : "text-muted-foreground hover:bg-muted"
          }`}
        >
          <Mail className="h-4 w-4" />
          <span>Lời mời nhận được</span>
          {incomingRequests.length > 0 && (
            <span className="flex h-5 min-w-5 items-center justify-center rounded-full bg-red-500 px-1.5 text-[11px] font-bold text-white">
              {incomingRequests.length}
            </span>
          )}
        </button>

        <button
          onClick={() => setActiveTab("outgoing")}
          className={`flex items-center gap-2 px-4 py-2 rounded-lg text-sm font-medium transition-colors ${
            activeTab === "outgoing"
              ? "bg-primary/10 text-primary"
              : "text-muted-foreground hover:bg-muted"
          }`}
        >
          <Send className="h-4 w-4" />
          <span>Đã gửi ({outgoingRequests.length})</span>
        </button>
      </div>

      {/* Main Content by Tab */}
      {isLoading ? (
        <div className="flex flex-col items-center justify-center py-16 text-muted-foreground">
          <Loader2 className="h-8 w-8 animate-spin mb-3 text-primary" />
          <p className="text-sm">Đang tải danh sách bạn bè...</p>
        </div>
      ) : activeTab === "friends" ? (
        <div className="space-y-4">
          {friends.length > 0 && (
            <div className="relative">
              <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
              <Input
                placeholder="Tìm bạn theo tên hoặc email..."
                value={searchQuery}
                onChange={(e) => setSearchQuery(e.target.value)}
                className="pl-9 h-10 max-w-md"
              />
            </div>
          )}

          {filteredFriends.length === 0 ? (
            <div className="text-center py-16 bg-card rounded-2xl border border-dashed border-border p-8">
              <div className="mx-auto flex h-14 w-14 items-center justify-center rounded-2xl bg-primary/10 text-primary mb-4">
                <Users className="h-7 w-7" />
              </div>
              <h3 className="text-base font-semibold text-foreground">
                {searchQuery ? "Không tìm thấy bạn bè nào khớp" : "Chưa có bạn bè nào"}
              </h3>
              <p className="text-sm text-muted-foreground max-w-md mx-auto mt-1 mb-6">
                {searchQuery
                  ? "Hãy thử tìm bằng từ khóa khác hoặc email khác."
                  : "Thêm bạn bè qua email để mời tham gia các cuộc hẹn và tìm địa điểm gặp mặt lý tưởng."}
              </p>
              {!searchQuery && (
                <Button onClick={() => setShowInviteModal(true)} className="gap-2">
                  <UserPlus className="h-4 w-4" />
                  <span>Gửi lời mời kết bạn ngay</span>
                </Button>
              )}
            </div>
          ) : (
            <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
              {filteredFriends.map((item) => {
                const friendName = getFriendName(item);
                const friendEmail = getFriendEmail(item);
                const avatar = getFriendAvatar(item);
                const createdDate = item.createdAt || item.since;

                return (
                  <div
                    key={item.id}
                    className="flex items-center justify-between p-4 bg-card rounded-xl border border-border hover:border-primary/40 transition-all shadow-sm"
                  >
                    <div className="flex items-center gap-3 min-w-0">
                      {avatar ? (
                        <img
                          src={
                            avatar.startsWith("http")
                              ? `/api/avatar?url=${encodeURIComponent(avatar)}`
                              : avatar
                          }
                          alt={friendName}
                          className="h-11 w-11 rounded-full object-cover border border-border shrink-0"
                        />
                      ) : (
                        <div className="flex h-11 w-11 items-center justify-center rounded-full bg-primary/10 text-primary font-bold text-sm shrink-0">
                          {friendName.slice(0, 2).toUpperCase()}
                        </div>
                      )}
                      <div className="min-w-0">
                        <p className="text-sm font-semibold text-foreground truncate">
                          {friendName}
                        </p>
                        <p className="text-xs text-muted-foreground truncate">
                          {friendEmail}
                        </p>
                        {createdDate && (
                          <span className="inline-flex items-center gap-1 text-[11px] text-muted-foreground mt-1">
                            <Clock className="h-3 w-3" />
                            Kết bạn từ {new Date(createdDate).toLocaleDateString("vi-VN")}
                          </span>
                        )}
                      </div>
                    </div>

                    <Button
                      variant="ghost"
                      size="sm"
                      disabled={processingId === item.id}
                      onClick={() => handleRemoveFriend(item.id, friendName)}
                      className="text-xs text-muted-foreground hover:text-red-600 hover:bg-red-50 dark:hover:bg-red-950/20"
                      title="Hủy kết bạn"
                    >
                      {processingId === item.id ? (
                        <Loader2 className="h-4 w-4 animate-spin" />
                      ) : (
                        <Trash2 className="h-4 w-4" />
                      )}
                    </Button>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      ) : activeTab === "incoming" ? (
        <div className="space-y-4">
          {incomingRequests.length === 0 ? (
            <div className="text-center py-16 bg-card rounded-2xl border border-dashed border-border p-8">
              <Mail className="h-10 w-10 text-muted-foreground mx-auto mb-3 opacity-60" />
              <h3 className="text-base font-semibold text-foreground">Không có lời mời nào</h3>
              <p className="text-sm text-muted-foreground mt-1">
                Khi có ai đó gửi lời mời kết bạn cho bạn, bạn sẽ nhận được thông báo tại đây.
              </p>
            </div>
          ) : (
            <div className="space-y-3 max-w-2xl">
              {incomingRequests.map((req) => {
                const sName = getSenderName(req);
                const sEmail = req.senderEmail || "";
                const sAvatar = req.senderAvatarUrl;

                return (
                  <div
                    key={req.id}
                    className="flex flex-col sm:flex-row sm:items-center justify-between gap-4 p-4 bg-card rounded-xl border border-border shadow-sm"
                  >
                    <div className="flex items-center gap-3 min-w-0">
                      {sAvatar ? (
                        <img
                          src={
                            sAvatar.startsWith("http")
                              ? `/api/avatar?url=${encodeURIComponent(sAvatar)}`
                              : sAvatar
                          }
                          alt={sName}
                          className="h-11 w-11 rounded-full object-cover border border-border shrink-0"
                        />
                      ) : (
                        <div className="flex h-11 w-11 items-center justify-center rounded-full bg-primary/10 text-primary font-bold text-sm shrink-0">
                          {sName.slice(0, 2).toUpperCase()}
                        </div>
                      )}
                      <div className="min-w-0">
                        <p className="text-sm font-semibold text-foreground truncate">
                          {sName}
                        </p>
                        <p className="text-xs text-muted-foreground truncate">
                          {sEmail}
                        </p>
                        {req.createdAt && (
                          <span className="text-[11px] text-muted-foreground">
                            Đã gửi lúc {new Date(req.createdAt).toLocaleDateString("vi-VN")}
                          </span>
                        )}
                      </div>
                    </div>

                    <div className="flex items-center gap-2 shrink-0">
                      <Button
                        size="sm"
                        disabled={processingId === req.id}
                        onClick={() => handleRespondRequest(req.id, "accept")}
                        className="bg-primary text-primary-foreground hover:bg-primary/90 text-xs gap-1.5 h-8"
                      >
                        {processingId === req.id ? (
                          <Loader2 className="h-3.5 w-3.5 animate-spin" />
                        ) : (
                          <Check className="h-3.5 w-3.5" />
                        )}
                        <span>Chấp nhận</span>
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        disabled={processingId === req.id}
                        onClick={() => handleRespondRequest(req.id, "decline")}
                        className="text-xs text-muted-foreground hover:text-red-600 hover:border-red-200 gap-1.5 h-8"
                      >
                        <X className="h-3.5 w-3.5" />
                        <span>Từ chối</span>
                      </Button>
                    </div>
                  </div>
                );
              })}
            </div>
          )}
        </div>
      ) : (
        <div className="space-y-4">
          {outgoingRequests.length === 0 ? (
            <div className="text-center py-16 bg-card rounded-2xl border border-dashed border-border p-8">
              <Send className="h-10 w-10 text-muted-foreground mx-auto mb-3 opacity-60" />
              <h3 className="text-base font-semibold text-foreground">Chưa gửi lời mời nào</h3>
              <p className="text-sm text-muted-foreground mt-1">
                Các lời mời kết bạn bạn đã gửi đang chờ đối phương phản hồi sẽ hiển thị ở đây.
              </p>
            </div>
          ) : (
            <div className="space-y-3 max-w-2xl">
              {outgoingRequests.map((req) => (
                <div
                  key={req.id}
                  className="flex items-center justify-between p-4 bg-card rounded-xl border border-border shadow-sm"
                >
                  <div className="flex items-center gap-3 min-w-0">
                    <div className="flex h-10 w-10 items-center justify-center rounded-full bg-muted text-muted-foreground font-semibold text-xs shrink-0">
                      <Mail className="h-4 w-4" />
                    </div>
                    <div className="min-w-0">
                      <p className="text-sm font-semibold text-foreground truncate">
                        {req.receiverEmail}
                      </p>
                      <p className="text-xs text-amber-600 dark:text-amber-400 flex items-center gap-1 font-medium">
                        <Clock className="h-3 w-3" />
                        Đang chờ phản hồi
                      </p>
                    </div>
                  </div>

                  <Button
                    variant="ghost"
                    size="sm"
                    disabled={processingId === req.id}
                    onClick={() => handleRemoveFriend(req.id, req.receiverEmail || "lời mời")}
                    className="text-xs text-muted-foreground hover:text-red-600 hover:bg-red-50 dark:hover:bg-red-950/20"
                    title="Thu hồi lời mời"
                  >
                    {processingId === req.id ? (
                      <Loader2 className="h-4 w-4 animate-spin" />
                    ) : (
                      <span className="flex items-center gap-1">
                        <X className="h-3.5 w-3.5" />
                        <span>Thu hồi</span>
                      </span>
                    )}
                  </Button>
                </div>
              ))}
            </div>
          )}
        </div>
      )}

      {/* Invite Modal */}
      {showInviteModal && (
        <div className="fixed inset-0 z-50 flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 animate-in fade-in duration-200">
          <div className="w-full max-w-md bg-card rounded-2xl border border-border shadow-2xl p-6 space-y-5 animate-in zoom-in-95 duration-200">
            <div className="flex items-start justify-between">
              <div>
                <h3 className="text-lg font-bold text-foreground flex items-center gap-2">
                  <UserPlus className="h-5 w-5 text-primary" />
                  Thêm bạn bè mới
                </h3>
                <p className="text-xs text-muted-foreground mt-1">
                  Nhập email của người bạn muốn kết bạn. Hệ thống sẽ gửi email kèm liên kết lời mời đến họ.
                </p>
              </div>
              <Button
                variant="ghost"
                size="icon"
                onClick={() => setShowInviteModal(false)}
                className="h-8 w-8 text-muted-foreground hover:text-foreground"
              >
                <X className="h-4 w-4" />
              </Button>
            </div>

            <form onSubmit={handleSendInvite} className="space-y-4">
              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-foreground">
                  Email bạn bè <span className="text-red-500">*</span>
                </label>
                <div className="relative">
                  <Mail className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                  <Input
                    type="email"
                    required
                    placeholder="nguoiban@example.com"
                    value={inviteEmail}
                    onChange={(e) => setInviteEmail(e.target.value)}
                    className="pl-9 text-sm"
                    autoFocus
                  />
                </div>
              </div>

              <div className="space-y-1.5">
                <label className="text-xs font-semibold text-foreground">
                  Lời nhắn gửi kèm (tùy chọn)
                </label>
                <Input
                  placeholder="Chào bạn, cùng kết nối trên LOSM nhé!"
                  value={inviteMessage}
                  onChange={(e) => setInviteMessage(e.target.value)}
                  className="text-sm"
                />
              </div>

              <div className="rounded-xl bg-muted/60 p-3 text-xs text-muted-foreground space-y-1 border border-border/50">
                <p className="flex items-center gap-1.5 font-medium text-foreground">
                  <Sparkles className="h-3.5 w-3.5 text-primary" />
                  Quy trình kích hoạt:
                </p>
                <ul className="list-disc list-inside space-y-0.5 text-[11px]">
                  <li>Hệ thống gửi thư mời đến email của người nhận.</li>
                  <li>Nếu chưa có tài khoản, họ sẽ được dẫn đến trang đăng ký.</li>
                  <li>Nếu đã có tài khoản, họ có thể chấp nhận ngay lập tức.</li>
                </ul>
              </div>

              <div className="flex items-center justify-end gap-2 pt-2">
                <Button
                  type="button"
                  variant="outline"
                  onClick={() => setShowInviteModal(false)}
                  disabled={isSendingInvite}
                >
                  Hủy
                </Button>
                <Button
                  type="submit"
                  disabled={isSendingInvite || !inviteEmail}
                  className="gap-2 bg-primary text-primary-foreground hover:bg-primary/90"
                >
                  {isSendingInvite ? (
                    <>
                      <Loader2 className="h-4 w-4 animate-spin" />
                      <span>Đang gửi mail...</span>
                    </>
                  ) : (
                    <>
                      <Send className="h-4 w-4" />
                      <span>Gửi lời mời</span>
                    </>
                  )}
                </Button>
              </div>
            </form>
          </div>
        </div>
      )}
    </div>
  );
}
