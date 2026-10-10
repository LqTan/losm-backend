"use client";

import { Button } from "@/components/ui/button";
import {
  Card,
  CardContent,
  CardHeader,
  CardTitle,
} from "@/components/ui/card";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { api, ApiError } from "@/lib/api";
import { getToken, getUser } from "@/lib/auth";
import type { FriendUser, Place } from "@/types/place";
import {
  Calendar,
  Check,
  Clock,
  Loader2,
  Mail,
  MapPin,
  Search,
  UserPlus,
  Users,
  X,
} from "lucide-react";
import { useEffect, useState } from "react";
import { toast } from "sonner";

interface Props {
  userLocation?: { lat: number; lng: number } | null;
  selectedPlace?: Place | null;
  onClose: () => void;
  onSelectPlaceOnMap?: (lat: number, lng: number, name: string) => void;
}

interface Participant {
  name: string;
  email: string;
}

export default function FairMeetingModal({
  userLocation,
  selectedPlace,
  onClose,
}: Props) {
  const [currentUser] = useState(() => getUser());

  // Meeting details
  const [customPlaceName, setCustomPlaceName] = useState(selectedPlace?.name ?? "");
  const [customAddress, setCustomAddress] = useState(selectedPlace?.address ?? "");
  const [meetingTitle, setMeetingTitle] = useState(
    selectedPlace ? `Hẹn gặp tại ${selectedPlace.name}` : "Hẹn gặp mặt bạn bè",
  );

  const [meetingDate, setMeetingDate] = useState(() => {
    const d = new Date();
    d.setDate(d.getDate() + 1);
    return d.toISOString().split("T")[0];
  });
  const [meetingTime, setMeetingTime] = useState("09:30");

  // Participants
  const [participants, setParticipants] = useState<Participant[]>(() => {
    if (currentUser?.email) {
      return [{ name: currentUser.username, email: currentUser.email }];
    }
    return [{ name: "Tôi", email: "host@example.com" }];
  });

  // Friend search
  const [friendQuery, setFriendQuery] = useState("");
  const [friendResults, setFriendResults] = useState<FriendUser[]>([]);
  const [searchingFriends, setSearchingFriends] = useState(false);
  const [customEmailInput, setCustomEmailInput] = useState("");

  // Submitting
  const [submitting, setSubmitting] = useState(false);
  const [successMessage, setSuccessMessage] = useState<string | null>(null);

  // Search friends from API
  useEffect(() => {
    const timer = setTimeout(async () => {
      setSearchingFriends(true);
      try {
        const token = getToken();
        const res = await api.get<FriendUser[]>(
          `/api/users/search?q=${encodeURIComponent(friendQuery.trim())}&limit=10`,
          token,
        );
        setFriendResults(res ?? []);
      } catch {
        setFriendResults([]);
      } finally {
        setSearchingFriends(false);
      }
    }, 250);

    return () => clearTimeout(timer);
  }, [friendQuery]);

  function handleToggleFriend(friend: FriendUser) {
    const exists = participants.some(
      (p) => p.email.toLowerCase() === friend.email.toLowerCase(),
    );
    if (exists) {
      setParticipants((prev) =>
        prev.filter((p) => p.email.toLowerCase() !== friend.email.toLowerCase()),
      );
    } else {
      setParticipants((prev) => [
        ...prev,
        { name: friend.username, email: friend.email },
      ]);
    }
  }

  function handleAddCustomEmail(e: React.FormEvent) {
    e.preventDefault();
    const email = customEmailInput.trim();
    if (!email || !email.includes("@")) {
      toast.error("Vui lòng nhập địa chỉ email hợp lệ.");
      return;
    }
    if (participants.some((p) => p.email.toLowerCase() === email.toLowerCase())) {
      toast.info("Email này đã có trong danh sách mời.");
      setCustomEmailInput("");
      return;
    }
    setParticipants((prev) => [
      ...prev,
      { name: email.split("@")[0], email },
    ]);
    setCustomEmailInput("");
  }

  function handleRemoveParticipant(email: string) {
    setParticipants((prev) => prev.filter((p) => p.email !== email));
  }

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();

    const placeName = selectedPlace?.name || customPlaceName.trim();
    if (!placeName) {
      toast.error("Vui lòng nhập hoặc chọn một địa điểm hẹn.");
      return;
    }

    const startDateTime = new Date(`${meetingDate}T${meetingTime}:00`);
    if (isNaN(startDateTime.getTime())) {
      toast.error("Thời gian hẹn không hợp lệ.");
      return;
    }

    const attendeeEmails = participants
      .map((p) => p.email.trim())
      .filter((e) => e.length > 0);

    if (attendeeEmails.length === 0) {
      toast.error("Vui lòng thêm ít nhất một email bạn bè để gửi lời mời.");
      return;
    }

    setSubmitting(true);
    try {
      const payload = {
        title: meetingTitle.trim() || `Cuộc hẹn tại ${placeName}`,
        placeName,
        address: selectedPlace?.address || customAddress.trim() || null,
        latitude: selectedPlace?.latitude ?? userLocation?.lat ?? 10.7769,
        longitude: selectedPlace?.longitude ?? userLocation?.lng ?? 106.7009,
        startAt: startDateTime.toISOString(),
        durationMinutes: 60,
        attendeeEmails,
        note: null,
        hostName: currentUser?.username ?? participants[0]?.name ?? "Người tổ chức",
      };

      const res = await api.post<{
        success: boolean;
        sentCount: number;
        message: string;
      }>("/api/meetings/schedule", payload, getToken());

      setSuccessMessage(res.message);
      toast.success(res.message);
    } catch (err) {
      toast.error(
        err instanceof ApiError
          ? err.message
          : "Gửi lời mời thất bại. Vui lòng thử lại.",
      );
    } finally {
      setSubmitting(false);
    }
  }

  return (
    <div className="fixed inset-0 z-[1050] flex items-center justify-center bg-black/50 p-4 backdrop-blur-xs">
      <Card className="flex max-h-[90vh] w-full max-w-lg flex-col overflow-hidden rounded-xl border bg-card text-card-foreground shadow-xl">
        {/* Header */}
        <CardHeader className="border-b px-5 py-3.5 bg-muted/20 flex flex-row items-center justify-between space-y-0">
          <div className="flex items-center gap-2">
            <Users className="h-4 w-4 text-muted-foreground" />
            <CardTitle className="text-base font-semibold text-foreground">
              {selectedPlace ? "Hẹn bạn bè tại đây" : "Tìm điểm hẹn chung"}
            </CardTitle>
          </div>
          <Button
            variant="ghost"
            size="icon"
            onClick={onClose}
            className="h-7 w-7 text-muted-foreground hover:text-foreground"
          >
            <X className="h-4 w-4" />
          </Button>
        </CardHeader>

        {/* Body Content */}
        <CardContent className="overflow-y-auto p-5 space-y-4">
          {successMessage ? (
            <div className="py-8 text-center space-y-3">
              <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-muted border">
                <Check className="h-6 w-6 text-foreground" />
              </div>
              <h3 className="text-base font-semibold text-foreground">
                Đã tạo lịch hẹn thành công!
              </h3>
              <p className="text-xs text-muted-foreground max-w-sm mx-auto">
                {successMessage}. File lịch hẹn (.ics) đã được gửi tới email của các bạn tham gia.
              </p>
              <div className="pt-2">
                <Button size="sm" onClick={onClose} className="text-xs px-4">
                  Đóng
                </Button>
              </div>
            </div>
          ) : (
            <form onSubmit={handleSubmit} className="space-y-4">
              {/* Place Info */}
              <div className="rounded-lg border bg-muted/30 p-3 space-y-1">
                <div className="text-xs text-muted-foreground flex items-center gap-1.5">
                  <MapPin className="h-3.5 w-3.5 text-muted-foreground shrink-0" />
                  <span className="font-medium text-foreground">
                    {selectedPlace ? selectedPlace.name : "Điểm hẹn"}
                  </span>
                </div>
                {selectedPlace?.address ? (
                  <p className="text-xs text-muted-foreground pl-5 line-clamp-1">
                    {selectedPlace.address}
                  </p>
                ) : (
                  <div className="pt-1.5 space-y-1.5">
                    <Input
                      placeholder="Nhập tên quán / địa điểm..."
                      value={customPlaceName}
                      onChange={(e) => setCustomPlaceName(e.target.value)}
                      className="h-8 text-xs bg-background"
                      required
                    />
                    <Input
                      placeholder="Địa chỉ (tùy chọn)..."
                      value={customAddress}
                      onChange={(e) => setCustomAddress(e.target.value)}
                      className="h-8 text-xs bg-background"
                    />
                  </div>
                )}
              </div>

              {/* Title Input */}
              <div className="space-y-1.5">
                <Label className="text-xs text-muted-foreground">Tiêu đề cuộc hẹn</Label>
                <Input
                  value={meetingTitle}
                  onChange={(e) => setMeetingTitle(e.target.value)}
                  placeholder="Ví dụ: Cafe cuối tuần, Gặp mặt..."
                  className="h-9 text-xs"
                  required
                />
              </div>

              {/* Date & Time */}
              <div className="grid grid-cols-2 gap-3">
                <div className="space-y-1.5">
                  <Label className="text-xs text-muted-foreground flex items-center gap-1">
                    <Calendar className="h-3 w-3 text-muted-foreground" />
                    Ngày hẹn
                  </Label>
                  <Input
                    type="date"
                    value={meetingDate}
                    onChange={(e) => setMeetingDate(e.target.value)}
                    className="h-9 text-xs"
                    required
                  />
                </div>
                <div className="space-y-1.5">
                  <Label className="text-xs text-muted-foreground flex items-center gap-1">
                    <Clock className="h-3 w-3 text-muted-foreground" />
                    Giờ hẹn
                  </Label>
                  <Input
                    type="time"
                    value={meetingTime}
                    onChange={(e) => setMeetingTime(e.target.value)}
                    className="h-9 text-xs"
                    required
                  />
                </div>
              </div>

              {/* Friends Section */}
              <div className="space-y-2 pt-1 border-t">
                <div className="flex items-center justify-between">
                  <Label className="text-xs text-muted-foreground flex items-center gap-1.5">
                    <Mail className="h-3.5 w-3.5 text-muted-foreground" />
                    Danh sách người tham gia ({participants.length})
                  </Label>
                </div>

                {/* Selected participant badges */}
                <div className="flex flex-wrap gap-1.5 min-h-[30px]">
                  {participants.map((p) => (
                    <span
                      key={p.email}
                      className="inline-flex items-center gap-1 rounded-md bg-muted px-2 py-0.5 text-xs text-foreground border"
                    >
                      <span>{p.name}</span>
                      <button
                        type="button"
                        onClick={() => handleRemoveParticipant(p.email)}
                        className="text-muted-foreground hover:text-foreground ml-0.5"
                      >
                        ×
                      </button>
                    </span>
                  ))}
                </div>

                {/* Search friend input */}
                <div className="relative">
                  <Search className="absolute left-2.5 top-2.5 h-3.5 w-3.5 text-muted-foreground" />
                  <Input
                    placeholder="Tìm theo tên hoặc email bạn bè..."
                    value={friendQuery}
                    onChange={(e) => setFriendQuery(e.target.value)}
                    className="h-8 pl-8 text-xs"
                  />
                  {searchingFriends && (
                    <Loader2 className="absolute right-2.5 top-2.5 h-3.5 w-3.5 animate-spin text-muted-foreground" />
                  )}
                </div>

                {/* Friend query search results */}
                {friendResults.length > 0 && (
                  <div className="max-h-28 overflow-y-auto rounded-md border divide-y text-xs bg-muted/10">
                    {friendResults.map((f) => {
                      const isSelected = participants.some(
                        (p) => p.email.toLowerCase() === f.email.toLowerCase(),
                      );
                      return (
                        <button
                          key={f.id}
                          type="button"
                          onClick={() => handleToggleFriend(f)}
                          className="flex w-full items-center justify-between p-2 text-left hover:bg-muted/40 transition"
                        >
                          <div>
                            <span className="font-medium text-foreground">{f.username}</span>
                            <span className="text-[11px] text-muted-foreground ml-1.5">({f.email})</span>
                          </div>
                          <span
                            className={`h-4 w-4 rounded border flex items-center justify-center text-[10px] ${
                              isSelected ? "bg-foreground text-background" : "border-muted-foreground/40"
                            }`}
                          >
                            {isSelected && "✓"}
                          </span>
                        </button>
                      );
                    })}
                  </div>
                )}

                {/* Or add direct email */}
                <div className="flex items-center gap-1.5 pt-1">
                  <Input
                    placeholder="Hoặc nhập email trực tiếp..."
                    value={customEmailInput}
                    onChange={(e) => setCustomEmailInput(e.target.value)}
                    className="h-8 text-xs flex-1"
                  />
                  <Button
                    type="button"
                    variant="outline"
                    size="sm"
                    onClick={handleAddCustomEmail}
                    className="h-8 text-xs px-2.5 shrink-0"
                  >
                    <UserPlus className="h-3.5 w-3.5 text-muted-foreground mr-1" />
                    Thêm
                  </Button>
                </div>
              </div>

              {/* Submit Buttons */}
              <div className="pt-3 border-t flex items-center justify-end gap-2">
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  onClick={onClose}
                  disabled={submitting}
                  className="text-xs h-9 px-3"
                >
                  Hủy
                </Button>
                <Button
                  type="submit"
                  size="sm"
                  disabled={submitting}
                  className="text-xs h-9 px-4 font-medium gap-1.5"
                >
                  {submitting && <Loader2 className="h-3.5 w-3.5 animate-spin" />}
                  <span>{submitting ? "Đang gửi..." : "Gửi lời mời lịch hẹn"}</span>
                </Button>
              </div>
            </form>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
