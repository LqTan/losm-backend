"use client";

import { useCallback, useEffect, useMemo, useState } from "react";
import {
  Bot,
  ChevronDown,
  ChevronUp,
  Compass,
  LogOut,
  MapPin,
  Plus,
  Search,
  Settings,
  Trash2,
  User,
  Users,
  X,
} from "lucide-react";
import { useRouter } from "next/navigation";
import { useQueryClient } from "@tanstack/react-query";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Badge } from "@/components/ui/badge";
import { Avatar, AvatarFallback } from "@/components/ui/avatar";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuLabel,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import {
  Popover,
  PopoverContent,
  PopoverTrigger,
} from "@/components/ui/popover";
import {
  Tooltip,
  TooltipContent,
  TooltipTrigger,
} from "@/components/ui/tooltip";
import { Skeleton } from "@/components/ui/skeleton";
import { InlineError } from "@/shared/ui/inline-error";
import { features } from "@/shared/config/features";
import { COPY } from "@/shared/config/copy";
import {
  searchPlaces as searchPlacesApi,
  searchMeetingPlaces as searchMeetingApi,
  fetchPlace as fetchPlaceApi,
  type GeoPoint,
  type Place,
} from "@/entities/place";
import { useCurrentLocation } from "@/features/get-current-location/use-current-location";
import { useSavePlace } from "@/features/save-place/use-save-place";
import { useSendMessage } from "@/features/send-message/use-send-message";
import { useSavedPlaces } from "@/features/saved-places-list/use-saved-places";
import { useUpdateProfile } from "@/features/update-profile/use-update-profile";
import { useCurrentUser } from "@/features/current-user/use-current-user";
import { logoutAction } from "@/features/auth";
import { PlaceList } from "@/widgets/place-list/place-list";
import { PlaceDetailDialog } from "@/widgets/place-detail-dialog/place-detail-dialog";
import { MeetingConfirmDialog } from "@/widgets/meeting-confirm-dialog/meeting-confirm-dialog";
import { MapPanelClient } from "@/widgets/map-panel/map-panel.lazy";
import { AiAssistantPanel } from "@/widgets/ai-assistant-panel/ai-assistant-panel";
import { SESSION_STORAGE_KEY } from "@/shared/config/constants";
import {
  asSessionId,
  type SessionId,
} from "@/shared/lib/brands";
import type { AttachedPlace, ChatMessage } from "@/entities/chat-session/model";
import { SAVED_PLACES_LIST_QUERY_KEY } from "@/features/saved-places-list/use-saved-places";
import { SESSION_LIST_QUERY_KEY } from "@/features/session-list/use-session-list";
import { upsertMessage } from "@/widgets/chat-thread";
import { toast } from "sonner";

type Mode = "one" | "many";

export interface MainViewProps {
  readonly initialUser: {
    readonly id: string;
    readonly username: string;
    readonly email: string;
    readonly displayName: string;
  };
}

export function MainView({ initialUser }: MainViewProps) {
  const router = useRouter();
  const qc = useQueryClient();
  const user = useCurrentUser({
    id: initialUser.id as never,
    email: initialUser.email,
    username: initialUser.username,
    displayName: initialUser.displayName,
    createdAt: new Date().toISOString(),
  });
  const profile = useUpdateProfile();

  const [mode, setMode] = useState<Mode>("one");
  const [query, setQuery] = useState("");
  const [radiusKm, setRadiusKm] = useState<number>(features.defaultRadiusKm);
  const [results, setResults] = useState<readonly Place[]>([]);
  const [searching, setSearching] = useState(false);
  const [searchError, setSearchError] = useState<string | null>(null);
  const [highlightedPlaceId, setHighlightedPlaceId] = useState<string | null>(null);
  const [activePlace, setActivePlace] = useState<AttachedPlace | null>(null);

  const [listOpen, setListOpen] = useState(true);

  const [aiOpen, setAiOpen] = useState(false);
  const [sessionId, setSessionId] = useState<SessionId | null>(null);
  const [messages, setMessages] = useState<readonly ChatMessage[]>([]);
  const [pendingActionId, setPendingActionId] = useState<string | null>(null);
  const [pendingPlaceName, setPendingPlaceName] = useState<string>("");

  const [origins, setOrigins] = useState<readonly GeoPoint[]>([
    { lat: features.defaultLat, lon: features.defaultLon },
  ]);

  const loc = useCurrentLocation(false);

  useEffect(() => {
    if (typeof window === "undefined") return;
    const stored = window.localStorage.getItem(SESSION_STORAGE_KEY);
    if (stored) setSessionId(asSessionId(stored));
  }, []);

  const handleSession = useCallback((id: SessionId) => {
    setSessionId(id);
    if (typeof window !== "undefined") {
      window.localStorage.setItem(SESSION_STORAGE_KEY, id);
    }
  }, []);

  const handleEvent = useCallback((msg: ChatMessage) => {
    setMessages((prev) => upsertMessage(prev, msg));
  }, []);

  const { streaming, error: sendError, latestStep, send, stop } = useSendMessage({
    sessionId,
    onSession: handleSession,
    onEvent: handleEvent,
    location: {
      lat: loc.lat ?? features.defaultLat,
      lon: loc.lon ?? features.defaultLon,
    },
  });

  const runSearch = useCallback(async () => {
    const q = query.trim();
    if (!q) return;
    setSearching(true);
    setSearchError(null);
    setListOpen(true);
    try {
      const lat = loc.lat ?? features.defaultLat;
      const lon = loc.lon ?? features.defaultLon;
      const data =
        mode === "one"
          ? await searchPlacesApi({
              query: q,
              lat,
              lon,
              radiusKm,
            })
          : await searchMeetingApi({
              query: q,
              attendees: origins,
              radiusKm,
            });
      setResults(data);
    } catch {
      setSearchError("Không tìm được. Vui lòng thử lại.");
      setResults([]);
    } finally {
      setSearching(false);
    }
  }, [query, mode, loc.lat, loc.lon, radiusKm, origins]);

  const placeList = useMemo<AttachedPlace[]>(() => {
    return results.map((p) => ({
      id: p.id,
      name: p.name,
      address: p.address,
      location: p.location,
      category: p.categories[0] ?? null,
      openingHours: null,
      distanceKm: p.distanceKm ?? null,
      finalScore: p.finalScore ?? null,
    }));
  }, [results]);

  const saved = useSavedPlaces();
  const savedSet = useMemo(
    () => new Set(saved.items.map((s) => s.placeId)),
    [saved.items],
  );

  const { save: savePlace } = useSavePlace();

  const handleSelect = useCallback((p: Place) => {
    setActivePlace({
      id: p.id,
      name: p.name,
      address: p.address,
      location: p.location,
      category: p.categories[0] ?? null,
      openingHours: null,
      distanceKm: p.distanceKm ?? null,
      finalScore: p.finalScore ?? null,
    });
  }, []);

  const handleJumpSaved = useCallback(
    async (savedItem: {
      placeId: string;
      name: string | null;
      address: string | null;
    }) => {
      try {
        const place = await fetchPlaceApi(savedItem.placeId);
        if (place) {
          setActivePlace({
            id: place.id,
            name: place.name,
            address: place.address,
            location: place.location,
            category: place.categories[0] ?? null,
            openingHours: null,
            distanceKm: place.distanceKm ?? null,
            finalScore: place.finalScore ?? null,
          });
          return;
        }
      } catch {
        /* fallthrough */
      }
      toast.message(
        savedItem.name
          ? `Mở trợ lý để đặt lịch tại "${savedItem.name}".`
          : "Mở trợ lý để đặt lịch tại địa điểm đã lưu.",
      );
      setAiOpen(true);
      if (savedItem.name) {
        window.dispatchEvent(
          new CustomEvent("losm:set-ai-input", {
            detail: COPY.chat.bookPrompt(savedItem.name),
          }),
        );
      }
    },
    [],
  );

  const handleBookFromDetail = useCallback((place: AttachedPlace) => {
    setActivePlace(null);
    setAiOpen(true);
    const prompt = COPY.chat.bookPrompt(place.name);
    window.dispatchEvent(
      new CustomEvent("losm:set-ai-input", { detail: prompt }),
    );
    toast.message("Trợ lý AI đã sẵn sàng nhận yêu cầu đặt lịch.");
  }, []);

  const handleConfirmAction = useCallback(
    (actionId: string, placeName: string) => {
      setPendingActionId(actionId);
      setPendingPlaceName(placeName);
    },
    [],
  );

  const handleLogout = useCallback(async () => {
    await logoutAction();
    void qc.invalidateQueries({ queryKey: [...SESSION_LIST_QUERY_KEY] });
    void qc.invalidateQueries({ queryKey: [...SAVED_PLACES_LIST_QUERY_KEY] });
    router.replace("/login");
    router.refresh();
  }, [router, qc]);

  const handleGoToProfile = useCallback(() => {
    router.push("/profile");
  }, [router]);

  const center = {
    lat: loc.lat ?? features.defaultLat,
    lon: loc.lon ?? features.defaultLon,
  };

  return (
    <div className="relative h-dvh overflow-hidden bg-muted/30">
      <div className="absolute inset-0">
        <MapPanelClient
          places={placeList.map((p) => ({
            id: p.id,
            name: p.name,
            address: p.address,
            location: p.location,
          }))}
          center={center}
          {...(highlightedPlaceId ? { highlightedId: highlightedPlaceId } : {})}
          onSelect={(p) => {
            setHighlightedPlaceId(p.id);
            const found = placeList.find((x) => x.id === p.id);
            if (found) setActivePlace(found);
          }}
        />
      </div>

      <header className="absolute top-0 left-0 right-0 z-30 flex h-16 items-center gap-2 border-b bg-background/95 px-3 shadow-sm backdrop-blur supports-[backdrop-filter]:bg-background/80">
        <div className="flex items-center gap-2 pr-2">
          <Bot size={22} strokeWidth={1.5} aria-hidden className="text-foreground" />
          <span className="hidden text-[15px] font-semibold tracking-tight sm:inline">
            {COPY.app.title}
          </span>
        </div>

        <Tabs
          value={mode}
          onValueChange={(v) => setMode(v as Mode)}
          className="hidden md:block"
        >
          <TabsList>
            <TabsTrigger value="one" className="gap-1.5">
              <Compass size={12} strokeWidth={1.5} />
              <span className="hidden lg:inline">{COPY.search.tabOne}</span>
            </TabsTrigger>
            <TabsTrigger value="many" className="gap-1.5">
              <Users size={12} strokeWidth={1.5} />
              <span className="hidden lg:inline">{COPY.search.tabMany}</span>
            </TabsTrigger>
          </TabsList>
        </Tabs>

        <OriginPopover
          mode={mode}
          lat={loc.lat}
          lon={loc.lon}
          locLoading={loc.loading}
          onRequestLocation={() => loc.request()}
          origins={origins}
          onAddOrigin={() =>
            setOrigins((arr) => [
              ...arr,
              {
                lat: features.defaultLat + (Math.random() - 0.5) * 0.05,
                lon: features.defaultLon + (Math.random() - 0.5) * 0.05,
              },
            ])
          }
          onRemoveOrigin={(idx) =>
            setOrigins((arr) => arr.filter((_, i) => i !== idx))
          }
        />

        <div className="relative flex flex-1 items-center">
          <Search
            size={16}
            strokeWidth={1.5}
            aria-hidden
            className="pointer-events-none absolute left-3 text-muted-foreground"
          />
          <Input
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={COPY.search.placeholder}
            onKeyDown={(e) => {
              if (e.key === "Enter") void runSearch();
            }}
            className="h-10 flex-1 pl-9 pr-20 text-[14px] shadow-sm"
          />
          <div className="absolute right-1 flex items-center gap-1">
            <Input
              type="number"
              min={1}
              max={20}
              value={radiusKm}
              onChange={(e) => setRadiusKm(Number(e.target.value) || 1)}
              className="h-8 w-14 px-2 text-center text-[12px]"
              aria-label={COPY.search.radius(radiusKm)}
            />
            <Button
              type="button"
              size="icon-sm"
              aria-label={COPY.search.button}
              onClick={() => void runSearch()}
              disabled={!query.trim() || searching}
            >
              <Search size={14} strokeWidth={1.5} />
            </Button>
          </div>
        </div>

        <SavedPopover
          saved={saved}
          onUnsave={(placeId) => void saved.unsave(placeId)}
          onJump={(item) => void handleJumpSaved(item)}
        />

        <DropdownMenu modal={false}>
          <Tooltip>
            <TooltipTrigger
              render={
                <DropdownMenuTrigger
                  render={
                    <Button
                      type="button"
                      variant="ghost"
                      size="icon-sm"
                      aria-label="Tài khoản"
                      className="rounded-full"
                    >
                      <Avatar className="size-7">
                        <AvatarFallback className="bg-primary text-primary-foreground text-[11px]">
                          {(user.data?.user.displayName ?? user.data?.user.username ?? "U")
                            .charAt(0)
                            .toUpperCase()}
                        </AvatarFallback>
                      </Avatar>
                    </Button>
                  }
                />
              }
            />
            <TooltipContent>
              {user.data?.user.displayName || user.data?.user.username || "Tài khoản"}
            </TooltipContent>
          </Tooltip>
          <DropdownMenuContent align="end" sideOffset={8} className="z-50 w-56">
            <DropdownMenuLabel>
              <div className="flex flex-col">
                <span className="text-[13px] font-semibold">
                  {user.data?.user.displayName || user.data?.user.username}
                </span>
                <span className="text-[11px] font-normal text-muted-foreground">
                  {user.data?.user.email ?? ""}
                </span>
              </div>
            </DropdownMenuLabel>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={handleGoToProfile}>
              <User size={14} strokeWidth={1.5} />
              {COPY.nav.profile}
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => router.push("/assistant")}>
              <Bot size={14} strokeWidth={1.5} />
              {COPY.search.aiAssistant}
            </DropdownMenuItem>
            <DropdownMenuItem onClick={() => router.push("/settings")}>
              <Settings size={14} strokeWidth={1.5} />
              Cài đặt
            </DropdownMenuItem>
            <DropdownMenuSeparator />
            <DropdownMenuItem onClick={() => void handleLogout()}>
              <LogOut size={14} strokeWidth={1.5} />
              {COPY.nav.logout}
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </header>

      <div
        className={
          "absolute bottom-4 left-4 right-4 z-20 flex max-h-[55vh] flex-col overflow-hidden rounded-xl border bg-background/95 shadow-xl backdrop-blur transition-all duration-200 sm:right-auto sm:w-[420px] " +
          (listOpen ? "" : "max-h-12")
        }
      >
        <button
          type="button"
          onClick={() => setListOpen((s) => !s)}
          className="flex h-12 shrink-0 items-center justify-between gap-2 border-b bg-background/90 px-3 text-left"
          aria-label={listOpen ? "Thu gọn danh sách" : "Mở rộng danh sách"}
        >
          <div className="flex items-center gap-2">
            <span className="text-[13px] font-semibold">
              {query.trim()
                ? COPY.search.resultsFor(query.trim())
                : "Địa điểm"}
            </span>
            <Badge variant="outline">{results.length}</Badge>
          </div>
          <div className="flex items-center gap-2">
            {listOpen ? (
              <ChevronDown size={14} strokeWidth={1.5} aria-hidden />
            ) : (
              <ChevronUp size={14} strokeWidth={1.5} aria-hidden />
            )}
          </div>
        </button>
        {listOpen ? (
          <div className="flex-1 overflow-auto p-3">
            {searchError ? (
              <InlineError message={searchError} />
            ) : searching ? (
              <div className="flex flex-col gap-2">
                <Skeleton className="h-20 w-full" />
                <Skeleton className="h-20 w-full" />
                <Skeleton className="h-20 w-full" />
              </div>
            ) : (
              <PlaceList
                places={results}
                onSelect={handleSelect}
                onSave={(p) => void savePlace(p.id)}
                highlightedId={highlightedPlaceId}
                savedIds={savedSet}
                emptyMessage={COPY.search.noResults}
              />
            )}
          </div>
        ) : null}
      </div>

      <Tooltip>
        <TooltipTrigger
          render={
            <Button
              type="button"
              size="icon-lg"
              className="absolute bottom-6 right-6 z-20 rounded-full shadow-xl"
              onClick={() => setAiOpen(true)}
              aria-label={COPY.search.aiAssistant}
            >
              <Bot size={20} strokeWidth={1.5} />
            </Button>
          }
        />
        <TooltipContent>{COPY.search.aiAssistantHint}</TooltipContent>
      </Tooltip>

      <PlaceDetailDialog
        place={activePlace}
        onClose={() => setActivePlace(null)}
        onSave={async (p) => {
          await savePlace(p.id);
          setActivePlace(null);
        }}
        onBook={handleBookFromDetail}
      />

      <MeetingConfirmDialog
        actionId={pendingActionId}
        placeName={pendingPlaceName}
        onClose={() => setPendingActionId(null)}
        onConfirmed={() => {
          setPendingActionId(null);
          toast.success("Đã xác nhận lịch hẹn.");
        }}
      />

      <AiAssistantPanel
        open={aiOpen}
        onOpenChange={setAiOpen}
        messages={messages}
        streaming={streaming}
        {...(latestStep !== undefined ? { latestStep } : {})}
        error={sendError}
        onSend={(t) =>
          send(t).then(() => {
            setMessages((prev) =>
              upsertMessage(prev, {
                id: `user-${Date.now()}`,
                role: "user",
                content: t,
                createdAt: new Date().toISOString(),
              }),
            );
          })
        }
        onStop={stop}
        onConfirmAction={handleConfirmAction}
      />

      {user.data?.user && profile.error ? (
        <div className="fixed bottom-3 left-1/2 z-50 -translate-x-1/2 rounded-md bg-destructive px-3 py-1.5 text-[12px] text-destructive-foreground">
          {profile.error}
        </div>
      ) : null}
    </div>
  );
}

function OriginPopover({
  mode,
  lat,
  lon,
  locLoading,
  onRequestLocation,
  origins,
  onAddOrigin,
  onRemoveOrigin,
}: {
  readonly mode: Mode;
  readonly lat: number | null;
  readonly lon: number | null;
  readonly locLoading: boolean;
  readonly onRequestLocation: () => void;
  readonly origins: readonly GeoPoint[];
  readonly onAddOrigin: () => void;
  readonly onRemoveOrigin: (idx: number) => void;
}) {
  const label = mode === "one" ? "Vị trí" : `${origins.length} vị trí`;
  return (
    <Popover>
      <Tooltip>
        <TooltipTrigger
          render={
            <PopoverTrigger
              render={
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="gap-1"
                >
                  <MapPin size={12} strokeWidth={1.5} />
                  {label}
                </Button>
              }
            />
          }
        />
        <TooltipContent>Vị trí tìm kiếm</TooltipContent>
      </Tooltip>
      <PopoverContent align="start" className="w-72">
        {mode === "one" ? (
          <div className="flex flex-col gap-2 text-[12px]">
            <Label className="text-muted-foreground">Vị trí của bạn</Label>
            <div className="flex items-center justify-between gap-2 rounded-md border bg-muted/30 px-3 py-2">
              <span className="inline-flex items-center gap-1 truncate text-foreground">
                <MapPin size={12} strokeWidth={1.5} />
                {lat !== null
                  ? `${lat.toFixed(3)}, ${lon?.toFixed(3)}`
                  : `${features.defaultLat}, ${features.defaultLon}`}
              </span>
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onClick={onRequestLocation}
                disabled={locLoading}
              >
                {locLoading ? "Đang lấy…" : "Dùng GPS"}
              </Button>
            </div>
          </div>
        ) : (
          <div className="flex flex-col gap-2 text-[12px]">
            <Label className="text-muted-foreground">Vị trí tham dự</Label>
            {origins.map((o, idx) => (
              <div
                key={`${o.lat}-${o.lon}-${idx}`}
                className="flex items-center justify-between gap-2 rounded-md border bg-muted/30 px-3 py-2"
              >
                <span className="truncate font-mono text-foreground">
                  {o.lat.toFixed(3)}, {o.lon.toFixed(3)}
                </span>
                {origins.length > 1 ? (
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon-sm"
                    aria-label={COPY.search.removeOrigin}
                    onClick={() => onRemoveOrigin(idx)}
                  >
                    <X size={12} strokeWidth={1.5} />
                  </Button>
                ) : null}
              </div>
            ))}
            <Button
              type="button"
              variant="secondary"
              size="sm"
              onClick={onAddOrigin}
            >
              <Plus size={14} strokeWidth={1.5} />
              {COPY.search.addOrigin}
            </Button>
          </div>
        )}
      </PopoverContent>
    </Popover>
  );
}

function SavedPopover({
  saved,
  onUnsave,
  onJump,
}: {
  readonly saved: ReturnType<typeof useSavedPlaces>;
  readonly onUnsave: (placeId: string) => void;
  readonly onJump: (item: {
    placeId: string;
    name: string | null;
    address: string | null;
  }) => void;
}) {
  return (
    <Popover>
      <Tooltip>
        <TooltipTrigger
          render={
            <PopoverTrigger
              render={
                <Button
                  type="button"
                  variant="outline"
                  size="sm"
                  className="relative gap-1"
                  aria-label={COPY.profile.savedPlacesTitle}
                >
                  <BookmarkIcon />
                  {saved.items.length > 0 ? (
                    <Badge
                      variant="default"
                      className="ml-1 h-4 min-w-4 px-1 text-[10px]"
                    >
                      {saved.items.length}
                    </Badge>
                  ) : null}
                </Button>
              }
            />
          }
        />
        <TooltipContent>{COPY.profile.savedPlacesTitle}</TooltipContent>
      </Tooltip>
      <PopoverContent align="end" className="w-72 p-0">
        <div className="border-b p-3 text-[12px] font-medium text-muted-foreground">
          {COPY.profile.savedPlacesTitle}
        </div>
        <div className="max-h-80 overflow-auto p-1">
          {saved.loading ? (
            <div className="space-y-2 p-2">
              <Skeleton className="h-8 w-full" />
              <Skeleton className="h-8 w-3/4" />
              <Skeleton className="h-8 w-5/6" />
            </div>
          ) : saved.items.length === 0 ? (
            <p className="px-3 py-6 text-center text-[12px] text-muted-foreground">
              {COPY.profile.savedPlacesEmpty}
            </p>
          ) : (
            <ul className="flex flex-col">
              {saved.items.map((s) => (
                <li
                  key={s.savedPlaceId}
                  className="group flex items-center justify-between gap-2 rounded-md px-2 py-1.5 hover:bg-muted"
                >
                  <button
                    type="button"
                    onClick={() => onJump(s)}
                    className="flex-1 truncate text-left text-[13px]"
                  >
                    {s.name ?? "(không tên)"}
                  </button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon-sm"
                    aria-label="Bỏ lưu"
                    onClick={() => onUnsave(s.placeId)}
                    className="opacity-0 transition-opacity group-hover:opacity-100"
                  >
                    <Trash2 size={12} strokeWidth={1.5} />
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </div>
      </PopoverContent>
    </Popover>
  );
}

function BookmarkIcon() {
  return (
    <svg
      width={14}
      height={14}
      viewBox="0 0 24 24"
      fill="none"
      stroke="currentColor"
      strokeWidth={1.5}
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden
    >
      <path d="m19 21-7-4-7 4V5a2 2 0 0 1 2-2h10a2 2 0 0 1 2 2v16Z" />
    </svg>
  );
}
