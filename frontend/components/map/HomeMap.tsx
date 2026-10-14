"use client";

import dynamic from "next/dynamic";
import SearchBar from "@/components/search/SearchBar";
import ResultsSidebar from "@/components/search/ResultsSidebar";
import PlaceCard from "../place/PlaceCard";
import FairMeetingModal from "../meeting/FairMeetingModal";
import type { OriginItem } from "../meeting/GroupMeetingFinder";
import AiChatWidget, { type AttachedRoute } from "../ai/AiChatWidget";
import UserNavDropdown from "../user/UserNavDropdown";
import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import { getToken, getUser } from "@/lib/auth";
import type { StoredUser } from "@/lib/auth";
import type { Place as PlaceType, MeetingPlace } from "@/types/place";
import { toast } from "sonner";
import { Bot, List, Loader2, LocateFixed, Users, X } from "lucide-react";

const MapView = dynamic(() => import("./MapView"), {
  ssr: false,
  loading: () => (
    <div className="flex h-full w-full items-center justify-center bg-slate-100 text-slate-500 text-sm">
      <Loader2 className="mr-2 h-5 w-5 animate-spin" />
      Đang tải bản đồ...
    </div>
  ),
});

function getDistanceKm(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const R = 6371;
  const dLat = ((lat2 - lat1) * Math.PI) / 180;
  const dLon = ((lon2 - lon1) * Math.PI) / 180;
  const a =
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos((lat1 * Math.PI) / 180) *
      Math.cos((lat2 * Math.PI) / 180) *
      Math.sin(dLon / 2) *
      Math.sin(dLon / 2);
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  return R * c;
}

const DEFAULT_COORDS = { lat: 10.7769, lng: 106.7009 }; // TP. Hồ Chí Minh center

export default function HomeMap() {
  const [places, setPlaces] = useState<PlaceType[]>([]);
  const [loading, setLoading] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);
  const [selectedPlace, setSelectedPlace] = useState<PlaceType | null>(null);
  const [flyToTarget, setFlyToTarget] = useState<{ lat: number; lng: number } | null>(null);

  // Current logged in user
  const [currentUser, setCurrentUser] = useState<StoredUser | null>(() => {
    if (typeof window !== "undefined") {
      return getUser();
    }
    return null;
  });

  useEffect(() => {
    setCurrentUser(getUser());
  }, []);

  // User location
  const [userLocation, setUserLocation] = useState<{ lat: number; lng: number } | null>(null);

  // Unified AI Assistant state
  const [isAssistantOpen, setIsAssistantOpen] = useState(false);
  const [groupOrigins, setGroupOrigins] = useState<OriginItem[]>([]);
  const [centroid, setCentroid] = useState<{ lat: number; lng: number } | null>(null);
  const [activeMeetingPlace, setActiveMeetingPlace] = useState<MeetingPlace | null>(null);
  const [meetingResults, setMeetingResults] = useState<MeetingPlace[]>([]);

  // Fair Meeting Modal state
  const [isMeetingModalOpen, setIsMeetingModalOpen] = useState(false);
  const [meetingParticipants, setMeetingParticipants] = useState<Array<{ name: string; email: string }>>([]);

  // Docked sidebar open/close state
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);
  const [locating, setLocating] = useState(false);

  // Real-world driving directions via OSRM
  const [navigationRoute, setNavigationRoute] = useState<{
    coordinates: [number, number][];
    distanceKm: number;
    durationMinutes: number;
  } | null>(null);
  const [loadingDirections, setLoadingDirections] = useState(false);
  const [selectedRouteOriginId, setSelectedRouteOriginId] = useState<string | null>(null);

  async function fallbackIpLocation(): Promise<boolean> {
    try {
      const res = await fetch("https://ipwho.is/");
      const data = await res.json();
      if (data && data.success && data.latitude && data.longitude) {
        const loc = { lat: Number(data.latitude), lng: Number(data.longitude) };
        setUserLocation(loc);
        setFlyToTarget(loc);
        toast.success(`Đã định vị theo mạng thiết bị (${data.city || "TP.HCM"})`);
        return true;
      }
    } catch {
      // ignore
    }
    return false;
  }

  // Locate User GPS
  function handleLocateUser() {
    if (locating) return;
    setLocating(true);
    toast.info("Đang xác định vị trí của bạn...");

    if (typeof window !== "undefined" && navigator.geolocation) {
      navigator.geolocation.getCurrentPosition(
        (pos) => {
          setLocating(false);
          const loc = { lat: pos.coords.latitude, lng: pos.coords.longitude };
          setUserLocation(loc);
          setFlyToTarget(loc);
          toast.success("Đã định vị vị trí GPS của bạn");
        },
        async (err) => {
          // Laptop thường không có chip GPS vệ tinh -> tự động fallback sang định vị mạng
          const ipSuccess = await fallbackIpLocation();
          setLocating(false);
          if (!ipSuccess) {
            if (err.code === 1) {
              toast.error("Trình duyệt đang chặn quyền vị trí. Vui lòng bật quyền vị trí ở thanh địa chỉ URL.");
            } else {
              toast.error("Không thể lấy vị trí GPS. Đã đặt về vị trí mặc định.");
              setUserLocation(DEFAULT_COORDS);
              setFlyToTarget(DEFAULT_COORDS);
            }
          }
        },
        { enableHighAccuracy: false, timeout: 6000, maximumAge: 60000 },
      );
    } else {
      void (async () => {
        const ipSuccess = await fallbackIpLocation();
        setLocating(false);
        if (!ipSuccess) {
          toast.error("Trình duyệt không hỗ trợ GPS");
        }
      })();
    }
  }

  // Request browser GPS position on mount (only locate, do NOT auto-search)
  useEffect(() => {
    if (typeof window !== "undefined" && navigator.geolocation) {
      navigator.geolocation.getCurrentPosition(
        (pos) => {
          const loc = {
            lat: pos.coords.latitude,
            lng: pos.coords.longitude,
          };
          setUserLocation(loc);
          setFlyToTarget(loc);
        },
        async () => {
          const ipSuccess = await fallbackIpLocation();
          if (!ipSuccess) {
            setUserLocation(DEFAULT_COORDS);
            setFlyToTarget(DEFAULT_COORDS);
          }
        },
        { enableHighAccuracy: false, timeout: 5000, maximumAge: 60000 },
      );
    } else {
      void fallbackIpLocation();
    }
  }, []);

  const loadPlaces = useCallback(
    async (queryText: string = "", lat?: number, lng?: number) => {
      const q = queryText.trim();
      if (!q) {
        setPlaces([]);
        setHasSearched(false);
        setIsSidebarOpen(false);
        return;
      }

      const searchLat = lat ?? userLocation?.lat ?? DEFAULT_COORDS.lat;
      const searchLng = lng ?? userLocation?.lng ?? DEFAULT_COORDS.lng;

      setLoading(true);
      try {
        const token = getToken();
        // 1. Try search API with spatial distance & ranking (limit 20)
        const params = new URLSearchParams({
          query: q,
          latitude: searchLat.toString(),
          longitude: searchLng.toString(),
          radiusKm: "10",
          limit: "20",
        });

        const data = await api.get<
          Array<{
            placeId?: string;
            id?: string;
            name: string;
            address?: string | null;
            latitude: number;
            longitude: number;
            category?: string | null;
            openingHours?: string | null;
            distanceKm?: number;
            finalScore?: number;
          }>
        >(`/api/search?${params.toString()}`, token);

        if (Array.isArray(data) && data.length > 0) {
          const mapped: PlaceType[] = data.map((item, idx) => ({
            id: item.placeId ?? item.id ?? `p_${idx}`,
            name: item.name,
            address: item.address ?? null,
            latitude: item.latitude,
            longitude: item.longitude,
            category: item.category ?? "Địa điểm",
            openingHours: item.openingHours ?? null,
            distanceKm: item.distanceKm,
            finalScore: item.finalScore,
          }));
          setPlaces(mapped);
          setHasSearched(true);
          setIsSidebarOpen(true);
        } else {
          // Fallback to /api/places if spatial search returned 0 items
          const fallbackData = await api.get<{
            items: Array<{
              id: string;
              name: string;
              address?: string | null;
              latitude: number;
              longitude: number;
              category?: string | null;
              openingHours?: string | null;
            }>;
          }>(`/api/places?page=1&pageSize=20&search=${encodeURIComponent(q)}`, token);

          if (fallbackData?.items && fallbackData.items.length > 0) {
            const mapped: PlaceType[] = fallbackData.items.map((item) => ({
              id: item.id,
              name: item.name,
              address: item.address ?? null,
              latitude: item.latitude,
              longitude: item.longitude,
              category: item.category ?? "Địa điểm",
              openingHours: item.openingHours ?? null,
            }));
            setPlaces(mapped);
            setHasSearched(true);
            setIsSidebarOpen(true);
          } else {
            setPlaces([]);
            setHasSearched(true);
            setIsSidebarOpen(true);
          }
        }
      } catch (err) {
        toast.error("Không thể tải danh sách địa điểm.");
      } finally {
        setLoading(false);
      }
    },
    [userLocation],
  );

  function handleSearch(query: string) {
    const q = query.trim();
    if (!q) {
      setPlaces([]);
      setHasSearched(false);
      setIsSidebarOpen(false);
      setSelectedPlace(null);
      return;
    }
    void loadPlaces(q);
  }

  function handleSelectPlace(place: PlaceType) {
    if (selectedPlace?.id !== place.id) {
      setNavigationRoute(null);
      setSelectedRouteOriginId(null);
    }
    setSelectedPlace(place);
    setFlyToTarget({ lat: place.latitude, lng: place.longitude });

    if (groupOrigins.length > 0) {
      const matched = meetingResults.find(
        (m) => m.placeId === place.id || m.placeId === (place as { placeId?: string }).placeId
      );
      if (matched) {
        setActiveMeetingPlace(matched);
      } else {
        const dists = groupOrigins.map((o) =>
          getDistanceKm(o.lat, o.lng, place.latitude, place.longitude)
        );
        const avgDist = dists.reduce((a, b) => a + b, 0) / (dists.length || 1);
        setActiveMeetingPlace({
          placeId: String(place.id),
          name: place.name,
          category: place.category ?? null,
          address: place.address ?? null,
          openingHours: place.openingHours ?? null,
          latitude: place.latitude,
          longitude: place.longitude,
          distanceKmByOrigin: dists,
          averageDistanceKm: avgDist,
          maxDistanceKm: Math.max(...dists, 0),
          spreadKm: Math.max(...dists, 0) - Math.min(...dists, 0),
          relevanceScore: place.relevanceScore ?? 1,
          distanceScore: place.distanceScore ?? 1,
          fairnessScore: place.fairnessScore ?? 1,
          finalScore: place.finalScore ?? 1,
          durationMinutesByOrigin: dists.map((km) => Math.max(1, Math.round((km / 25) * 60))),
        });
      }
    }
  }

  function handleSelectPlaceFromModalOrChat(
    lat: number,
    lng: number,
    name: string,
    extra?: Partial<PlaceType>
  ) {
    const dist = userLocation
      ? getDistanceKm(userLocation.lat, userLocation.lng, lat, lng)
      : undefined;

    const targetPlace: PlaceType = {
      id: extra?.id || `place_${Date.now()}`,
      name: extra?.name || name,
      latitude: lat,
      longitude: lng,
      address: extra?.address ?? null,
      category: extra?.category ?? "Địa điểm",
      openingHours: extra?.openingHours ?? null,
      distanceKm: extra?.distanceKm ?? dist,
    };

    setPlaces((prev) => {
      const existsIndex = prev.findIndex(
        (p) =>
          p.id === targetPlace.id ||
          (Math.abs(p.latitude - lat) < 0.0001 && Math.abs(p.longitude - lng) < 0.0001)
      );
      if (existsIndex >= 0) {
        const next = [...prev];
        next[existsIndex] = { ...next[existsIndex], ...targetPlace };
        return next;
      }
      return [targetPlace, ...prev];
    });

    handleSelectPlace(targetPlace);
  }

  async function handleGetDirections(place: PlaceType, customOrigin?: OriginItem) {
    let origin: { lat: number; lng: number } | null = null;
    let originLabel = "vị trí của bạn";

    if (customOrigin) {
      origin = { lat: customOrigin.lat, lng: customOrigin.lng };
      setSelectedRouteOriginId(customOrigin.id);
      originLabel = customOrigin.name;
    } else if (groupOrigins.length > 0) {
      origin = { lat: groupOrigins[0].lat, lng: groupOrigins[0].lng };
      setSelectedRouteOriginId(groupOrigins[0].id);
      originLabel = groupOrigins[0].name;
    } else {
      setSelectedRouteOriginId(null);
      origin = userLocation;
      if (!origin) {
        if (typeof window !== "undefined" && navigator.geolocation) {
          try {
            const pos = await new Promise<GeolocationPosition>((resolve, reject) => {
              navigator.geolocation.getCurrentPosition(resolve, reject, {
                enableHighAccuracy: false,
                timeout: 4000,
              });
            });
            origin = { lat: pos.coords.latitude, lng: pos.coords.longitude };
            setUserLocation(origin);
          } catch {
            origin = DEFAULT_COORDS;
          }
        } else {
          origin = DEFAULT_COORDS;
        }
      }
    }

    setLoadingDirections(true);
    try {
      const url = `https://router.project-osrm.org/route/v1/driving/${origin.lng},${origin.lat};${place.longitude},${place.latitude}?overview=full&geometries=geojson`;
      const res = await fetch(url);
      if (!res.ok) {
        throw new Error(`OSRM HTTP ${res.status}`);
      }
      const data = await res.json();
      if (data?.code === "Ok" && data.routes && data.routes.length > 0) {
        const bestRoute = data.routes[0];
        const coords = (bestRoute.geometry?.coordinates ?? []).map(
          ([lng, lat]: [number, number]) => [lat, lng] as [number, number]
        );
        const distanceKm = (bestRoute.distance ?? 0) / 1000;
        const durationMinutes = (bestRoute.duration ?? 0) / 60;

        setNavigationRoute({
          coordinates: coords,
          distanceKm,
          durationMinutes,
        });

        toast.success(
          `Đã tìm thấy lộ trình từ ${originLabel}: ${distanceKm.toFixed(1)} km (~${Math.round(durationMinutes)} phút lái xe)`
        );
      } else {
        toast.error(`Không tìm thấy tuyến đường khả dụng từ ${originLabel}.`);
      }
    } catch (err) {
      console.error("OSRM Route Error:", err);
      toast.error("Không thể lấy dữ liệu lộ trình từ hệ thống định tuyến OSRM.");
    } finally {
      setLoadingDirections(false);
    }
  }

  function handleClearDirections() {
    setNavigationRoute(null);
    setSelectedRouteOriginId(null);
  }

  // Xử lý khi người dùng mở link từ email mời gặp mặt:
  // Ví dụ: /?lat=10.77&lng=106.70&name=...&address=...&action=directions
  useEffect(() => {
    if (typeof window === "undefined") return;
    const params = new URLSearchParams(window.location.search);
    const latStr = params.get("lat");
    const lngStr = params.get("lng");
    const nameParam = params.get("name");
    const addressParam = params.get("address");
    const action = params.get("action");

    if (latStr && lngStr) {
      const lat = parseFloat(latStr);
      const lng = parseFloat(lngStr);
      if (!isNaN(lat) && !isNaN(lng)) {
        const placeName = nameParam ? decodeURIComponent(nameParam) : "Địa điểm hẹn";
        const placeAddress = addressParam ? decodeURIComponent(addressParam) : null;
        const targetPlace: PlaceType = {
          id: `meeting_target_${Date.now()}`,
          name: placeName,
          address: placeAddress,
          latitude: lat,
          longitude: lng,
          category: "Điểm hẹn",
        };

        setPlaces((prev) => {
          const exists = prev.some(
            (p) => Math.abs(p.latitude - lat) < 0.0001 && Math.abs(p.longitude - lng) < 0.0001
          );
          return exists ? prev : [targetPlace, ...prev];
        });

        setSelectedPlace(targetPlace);
        setFlyToTarget({ lat, lng });

        if (action === "directions") {
          toast.info(`Đang mở chỉ đường tới ${placeName} trên bản đồ LOSM...`);
          void handleGetDirections(targetPlace);
        } else {
          toast.info(`Đang xem vị trí cuộc hẹn: ${placeName}`);
        }
      }
    }
  }, []);

  function handleAiRouteReceived(route: AttachedRoute) {
    setNavigationRoute({
      coordinates: route.coordinates,
      distanceKm: route.distanceKm,
      durationMinutes: route.durationMinutes,
    });
    const label = route.memberName || route.originName || "thành viên";
    const dest = route.destinationName || "địa điểm";
    toast.success(`Đã hiển thị đường đi của ${label} đến ${dest} (${route.distanceKm.toFixed(1)} km)`);
  }

  function handleGroupSearchResults(
    results: MeetingPlace[],
    centroidCoord: { lat: number; lng: number } | null,
    origins: OriginItem[]
  ) {
    setGroupOrigins(origins);
    setCentroid(centroidCoord);
    setMeetingResults(results);

    const mappedPlaces: PlaceType[] = results.map((r) => ({
      id: r.placeId,
      placeId: r.placeId,
      name: r.name,
      category: r.category,
      address: r.address,
      latitude: r.latitude,
      longitude: r.longitude,
      distanceKm: r.averageDistanceKm,
      relevanceScore: r.relevanceScore,
      distanceScore: r.distanceScore,
      fairnessScore: r.fairnessScore,
      finalScore: r.finalScore,
    }));

    setPlaces(mappedPlaces);
    setHasSearched(true);
    setIsSidebarOpen(false);

    if (results.length > 0) {
      setActiveMeetingPlace(results[0]);
      setSelectedPlace(mappedPlaces[0]);
    }

    if (centroidCoord) {
      setFlyToTarget(centroidCoord);
    }
  }

  function handleSelectGroupMeetingPlace(place: MeetingPlace, shouldCloseModal = true) {
    setActiveMeetingPlace(place);
    const p: PlaceType = {
      id: place.placeId,
      placeId: place.placeId,
      name: place.name,
      category: place.category,
      address: place.address,
      latitude: place.latitude,
      longitude: place.longitude,
      distanceKm: place.averageDistanceKm,
      relevanceScore: place.relevanceScore,
      distanceScore: place.distanceScore,
      fairnessScore: place.fairnessScore,
      finalScore: place.finalScore,
    };
    setSelectedPlace(p);
    setFlyToTarget({ lat: place.latitude, lng: place.longitude });
    if (shouldCloseModal) {
      setIsAssistantOpen(false);
    }
  }

  function handleScheduleFromGroup(place: PlaceType, participants?: Array<{ name: string; email: string }>) {
    setSelectedPlace(place);
    if (participants && participants.length > 0) {
      setMeetingParticipants(participants);
    }
    setIsMeetingModalOpen(true);
  }

  function handleResetChatAndMap() {
    setPlaces([]);
    setHasSearched(false);
    setSelectedPlace(null);
    setIsSidebarOpen(false);
    setActiveMeetingPlace(null);
    setGroupOrigins([]);
    setMeetingResults([]);
    setCentroid(null);
    setNavigationRoute(null);
    setSelectedRouteOriginId(null);
  }

  function handleClearMeetingMode() {
    setActiveMeetingPlace(null);
    setGroupOrigins([]);
    setMeetingResults([]);
    setCentroid(null);
    setSelectedPlace(null);
    setNavigationRoute(null);
    setSelectedRouteOriginId(null);
  }

  return (
    <main className="fixed inset-0 overflow-hidden bg-slate-900">
      {/* Interactive Map */}
      <MapView
        places={places}
        selectedPlace={selectedPlace}
        userLocation={userLocation}
        onSelectPlace={handleSelectPlace}
        flyToTarget={flyToTarget}
        groupOrigins={groupOrigins}
        centroid={centroid}
        activeMeetingPlace={activeMeetingPlace}
        currentUserAvatar={currentUser?.avatarUrl}
        navigationRoute={navigationRoute}
      />

      {/* Top Search Bar & Controls */}
      <div className="absolute top-4 left-4 right-16 sm:right-20 mx-auto max-w-xl z-[990]">
        <SearchBar
          onSearch={handleSearch}
          loading={loading}
        />
      </div>

      {/* Floating Unified Assistant Trigger Button (Góc dưới bên phải - Mở Trợ lý AI) */}
      {!isAssistantOpen && (
        <button
          type="button"
          onClick={() => setIsAssistantOpen(true)}
          className="fixed right-5 bottom-20 z-[1000] flex items-center gap-3 rounded-2xl bg-card/95 border border-border shadow-lg backdrop-blur-md px-3.5 py-2.5 text-xs font-semibold text-foreground hover:bg-muted hover:border-primary/50 transition-all active:scale-95 group cursor-pointer"
          title="Mở Trợ lý AI"
        >
          <div className="flex h-8 w-8 items-center justify-center rounded-xl bg-gradient-to-tr from-blue-600 to-indigo-600 text-white shadow-xs group-hover:scale-105 transition">
            <Bot className="h-4 w-4" />
          </div>
          <div className="text-left pr-1">
            <div className="text-[12px] font-bold text-foreground leading-tight flex items-center gap-1.5">
              Trợ lý AI
              <span className="text-[9px] font-medium px-1 py-0.2 rounded bg-blue-100 text-blue-700">LOSM</span>
            </div>
            <div className="text-[10px] text-muted-foreground mt-0.5">
              Gợi ý địa điểm & Tìm điểm hẹn
            </div>
          </div>
        </button>
      )}

      {/* Active Meeting Place Quick Indicator Banner */}
      {activeMeetingPlace && (
        <div className="fixed top-20 left-1/2 -translate-x-1/2 z-[990] flex items-center gap-2 rounded-full border border-primary/30 bg-card/95 pl-4 pr-2 py-1.5 shadow-xl backdrop-blur-md text-xs animate-in fade-in slide-in-from-top-2">
          <span className="flex h-2.5 w-2.5 rounded-full bg-emerald-500 animate-pulse shrink-0" />
          <span className="font-medium text-foreground">
            Điểm hẹn: <span className="text-primary font-bold">{activeMeetingPlace.name}</span>
          </span>

          <div className="h-3.5 w-px bg-border mx-1" />

          <button
            type="button"
            onClick={() => {
              setIsAssistantOpen(true);
            }}
            className="font-semibold text-primary hover:underline flex items-center gap-0.5 text-xs cursor-pointer"
            title="Mở lại danh sách điểm hẹn trong Trợ lý AI"
          >
            <span>Danh sách</span>
          </button>

          <button
            type="button"
            onClick={handleClearMeetingMode}
            className="ml-1 flex h-6 w-6 items-center justify-center rounded-full text-muted-foreground hover:bg-destructive/10 hover:text-destructive transition cursor-pointer"
            title="Hủy xem điểm hẹn (xóa lộ trình & tâm điểm trên bản đồ)"
            aria-label="Hủy xem điểm hẹn"
          >
            <X className="h-3.5 w-3.5" />
          </button>
        </div>
      )}

      {/* Floating User Profile Dropdown */}
      <UserNavDropdown />

      {/* Docked Left Search Results Sidebar */}
      {hasSearched && places.length > 0 && isSidebarOpen && (
        <aside className="fixed left-0 top-0 bottom-0 z-[1000] w-full sm:w-[380px] flex flex-col bg-card border-r border-border shadow-2xl backdrop-blur-md animate-in slide-in-from-left duration-200">
          <div className="flex h-14 items-center justify-between border-b px-4 shrink-0">
            <div className="flex items-center gap-2">
              <span className="text-sm font-semibold text-foreground">Danh sách địa điểm</span>
              {loading && <Loader2 className="h-3.5 w-3.5 animate-spin text-muted-foreground" />}
            </div>
            <button
              type="button"
              onClick={() => setIsSidebarOpen(false)}
              className="flex h-8 w-8 items-center justify-center rounded-lg text-muted-foreground hover:text-foreground hover:bg-muted transition"
              title="Đóng danh sách"
              aria-label="Đóng danh sách"
            >
              <X className="h-4 w-4" />
            </button>
          </div>
          <div className="min-h-0 flex-1 overflow-y-auto">
            <ResultsSidebar
              places={places}
              onSelectPlace={handleSelectPlace}
            />
          </div>
        </aside>
      )}

      {/* Re-open Docked Sidebar Button */}
      {hasSearched && places.length > 0 && !isSidebarOpen && (
        <button
          type="button"
          onClick={() => setIsSidebarOpen(true)}
          className="fixed left-0 top-24 z-[990] flex items-center gap-2 rounded-r-xl border border-l-0 bg-card/95 px-3 py-2 text-xs font-medium text-foreground shadow-md backdrop-blur-md hover:bg-muted transition"
        >
          <List className="h-4 w-4 text-muted-foreground" />
          <span>Danh sách địa điểm</span>
        </button>
      )}

      {/* Dedicated Floating GPS Locate Button */}
      <button
        type="button"
        onClick={handleLocateUser}
        disabled={locating}
        className="fixed right-5 bottom-5 z-[1000] flex h-11 w-11 items-center justify-center rounded-full bg-card/95 border border-border shadow-md backdrop-blur-md text-foreground hover:bg-muted active:scale-95 transition-all disabled:opacity-60 cursor-pointer"
        title="Vị trí của tôi (GPS / Mạng)"
        aria-label="Vị trí của tôi (GPS / Mạng)"
      >
        <LocateFixed className={`h-5 w-5 text-foreground ${locating ? "animate-spin text-primary" : ""}`} />
      </button>

      {/* Bottom Place Card Detail */}
      {selectedPlace && (
        <PlaceCard
          place={selectedPlace}
          activeMeetingPlace={activeMeetingPlace}
          groupOrigins={groupOrigins}
          navigationRoute={navigationRoute}
          loadingDirections={loadingDirections}
          selectedRouteOriginId={selectedRouteOriginId}
          onClose={() => {
            setSelectedPlace(null);
            setNavigationRoute(null);
            setSelectedRouteOriginId(null);
          }}
          onScheduleMeeting={() => setIsMeetingModalOpen(true)}
          onGetDirections={handleGetDirections}
          onClearDirections={handleClearDirections}
        />
      )}

      {/* Fair Meeting & Schedule Modal */}
      {isMeetingModalOpen && (
        <FairMeetingModal
          userLocation={userLocation}
          selectedPlace={selectedPlace}
          initialParticipants={meetingParticipants}
          onClose={() => {
            setIsMeetingModalOpen(false);
            setMeetingParticipants([]);
          }}
          onSelectPlaceOnMap={handleSelectPlaceFromModalOrChat}
        />
      )}

      {/* Conversational AI Chat Widget (Tích hợp Điểm hẹn nhóm, phóng to full màn hình & tạo chat mới) */}
      <AiChatWidget
        userLocation={userLocation}
        onSelectPlaceOnMap={handleSelectPlaceFromModalOrChat}
        onOpenMeetingModal={() => setIsMeetingModalOpen(true)}
        onResetMap={handleResetChatAndMap}
        onGroupSearchResults={handleGroupSearchResults}
        onSelectMeetingPlace={handleSelectGroupMeetingPlace}
        onScheduleMeeting={handleScheduleFromGroup}
        onRouteReceived={handleAiRouteReceived}
        activeMeetingPlaceId={activeMeetingPlace?.placeId}
        isOpenControlled={isAssistantOpen}
        onCloseControlled={() => setIsAssistantOpen(false)}
        hideFloatingTrigger={true}
      />
    </main>
  );
}