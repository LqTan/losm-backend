"use client";

import { useEffect, useState } from "react";
import { Button } from "@/components/ui/button";
import { api } from "@/lib/api";
import { getToken } from "@/lib/auth";
import { Check, Loader2, UserCheck, UserX, Users, X } from "lucide-react";
import { toast } from "sonner";

export interface FriendRequestItem {
  id: string;
  token: string;
  senderId: string;
  senderName: string;
  senderEmail: string;
  createdAt: string;
}

interface Props {
  isOpen: boolean;
  onClose: () => void;
  onRequestsUpdated?: (count: number) => void;
}

export default function FriendRequestsModal({
  isOpen,
  onClose,
  onRequestsUpdated,
}: Props) {
  const [requests, setRequests] = useState<FriendRequestItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [processingId, setProcessingId] = useState<string | null>(null);

  useEffect(() => {
    if (isOpen) {
      loadRequests();
    }
  }, [isOpen]);

  async function loadRequests() {
    setLoading(true);
    try {
      const token = getToken();
      if (!token) return;
      const res = await api.get<{
        incoming: FriendRequestItem[];
        incomingCount: number;
      }>("/api/friends/requests", token);

      if (res?.incoming) {
        setRequests(res.incoming);
        onRequestsUpdated?.(res.incomingCount);
      }
    } catch (err) {
      console.error("Failed to load friend requests:", err);
    } finally {
      setLoading(false);
    }
  }

  async function handleRespond(req: FriendRequestItem, accept: boolean) {
    setProcessingId(req.id);
    try {
      const token = getToken();
      await api.post(
        "/api/friends/respond",
        { friendshipId: req.id, accept },
        token
      );

      if (accept) {
        toast.success(`Bạn và ${req.senderName} đã trở thành bạn bè!`);
      } else {
        toast.info("Đã từ chối lời mời kết bạn.");
      }

      const updated = requests.filter((r) => r.id !== req.id);
      setRequests(updated);
      onRequestsUpdated?.(updated.length);
    } catch {
      toast.error("Không thể xử lý yêu cầu. Vui lòng thử lại.");
    } finally {
      setProcessingId(null);
    }
  }

  if (!isOpen) return null;

  return (
    <div
      onClick={(e) => {
        if (e.target === e.currentTarget) onClose();
      }}
      className="fixed inset-0 z-[1100] flex items-center justify-center bg-black/60 backdrop-blur-sm p-4 animate-in fade-in duration-200"
    >
      <div className="relative w-full max-w-md bg-card border border-border rounded-2xl shadow-2xl overflow-hidden text-foreground">
        {/* Header */}
        <div className="flex items-center justify-between px-5 py-4 border-b border-border bg-muted/40">
          <div className="flex items-center gap-2.5">
            <div className="flex h-9 w-9 items-center justify-center rounded-xl bg-primary/10 text-primary">
              <Users className="h-4.5 w-4.5" />
            </div>
            <div>
              <h3 className="text-sm font-bold text-foreground">
                Lời mời kết bạn ({requests.length})
              </h3>
              <p className="text-[11px] text-muted-foreground">
                Danh sách người dùng muốn kết bạn với bạn
              </p>
            </div>
          </div>
          <button
            onClick={onClose}
            className="flex h-7 w-7 items-center justify-center rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted transition"
          >
            <X className="h-4 w-4" />
          </button>
        </div>

        {/* Content */}
        <div className="p-4 max-h-[60vh] overflow-y-auto">
          {loading ? (
            <div className="flex items-center justify-center py-8 text-xs text-muted-foreground gap-2">
              <Loader2 className="h-4 w-4 animate-spin text-primary" />
              Đang tải danh sách lời mời...
            </div>
          ) : requests.length === 0 ? (
            <div className="py-8 text-center space-y-2">
              <div className="flex h-12 w-12 mx-auto items-center justify-center rounded-full bg-muted text-muted-foreground">
                <UserCheck className="h-6 w-6" />
              </div>
              <p className="text-xs font-medium text-foreground">
                Không có lời mời kết bạn nào
              </p>
              <p className="text-[11px] text-muted-foreground">
                Khi ai đó gửi lời mời kết bạn cho bạn, yêu cầu sẽ xuất hiện tại đây.
              </p>
            </div>
          ) : (
            <div className="space-y-2.5">
              {requests.map((req) => (
                <div
                  key={req.id}
                  className="p-3 rounded-xl border border-border bg-muted/30 flex items-center justify-between gap-3"
                >
                  <div className="flex items-center gap-2.5 min-w-0 flex-1">
                    <div className="flex h-9 w-9 items-center justify-center rounded-full bg-primary/20 text-primary font-bold text-xs shrink-0">
                      {(req.senderName || req.senderEmail || "U").charAt(0).toUpperCase()}
                    </div>
                    <div className="min-w-0 flex-1">
                      <div className="text-xs font-bold text-foreground truncate">
                        {req.senderName || req.senderEmail?.split("@")[0] || "Người dùng"}
                      </div>
                      <div className="text-[11px] text-muted-foreground truncate">
                        {req.senderEmail}
                      </div>
                    </div>
                  </div>

                  <div className="flex items-center gap-1.5 shrink-0">
                    <Button
                      size="sm"
                      onClick={() => handleRespond(req, true)}
                      disabled={processingId === req.id}
                      className="h-7 text-xs px-2.5 gap-1 bg-primary text-primary-foreground hover:bg-primary/90"
                    >
                      {processingId === req.id ? (
                        <Loader2 className="h-3 w-3 animate-spin" />
                      ) : (
                        <Check className="h-3.5 w-3.5" />
                      )}
                      <span>Đồng ý</span>
                    </Button>
                    <Button
                      size="sm"
                      variant="outline"
                      onClick={() => handleRespond(req, false)}
                      disabled={processingId === req.id}
                      className="h-7 text-xs px-2 text-muted-foreground hover:text-destructive hover:border-destructive/30"
                    >
                      <UserX className="h-3.5 w-3.5" />
                    </Button>
                  </div>
                </div>
              ))}
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
