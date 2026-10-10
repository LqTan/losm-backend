"use client";

import dynamic from "next/dynamic";
import SearchBar from "@/components/search/SearchBar";
import ResultsSidebar from "@/components/search/ResultsSidebar";
import PlaceCard from "../place/PlaceCard";
import FairMeetingModal from "../meeting/FairMeetingModal";
import AiChatWidget from "../ai/AiChatWidget";
import UserNavDropdown from "../user/UserNavDropdown";
import { useCallback, useEffect, useState } from "react";
import { api } from "@/lib/api";
import { getToken } from "@/lib/auth";
import type { Place as PlaceType } from "@/types/place";
import { toast } from "sonner";
import { List, Loader2, LocateFixed, X } from "lucide-react";

const MapView = dynamic(() => import("./MapView"), {
  ssr: false,
  loading: () => (
    <div className="flex h-full w-full items-center justify-center bg-slate-100 text-slate-500 text-sm">
      <Loader2 className="mr-2 h-5 w-5 animate-spin" />
      Đang tải bản đồ...
    </div>
  ),
});

const DEFAULT_COORDS = { lat: 10.7769, lng: 106.7009 }; // TP. Hồ Chí Minh center

export default function HomeMap() {
  const [places, setPlaces] = useState<PlaceType[]>([]);
  const [loading, setLoading] = useState(false);
  const [hasSearched, setHasSearched] = useState(false);
  const [selectedPlace, setSelectedPlace] = useState<PlaceType | null>(null);
  const [flyToTarget, setFlyToTarget] = useState<{ lat: number; lng: number } | null>(null);

  // User location
  const [userLocation, setUserLocation] = useState<{ lat: number; lng: number } | null>(null);

  // Fair Meeting Modal state
  const [isMeetingModalOpen, setIsMeetingModalOpen] = useState(false);

  // Docked sidebar open/close state
  const [isSidebarOpen, setIsSidebarOpen] = useState(false);

  // Locate User GPS
  function handleLocateUser() {
    if (typeof window !== "undefined" && navigator.geolocation) {
      navigator.geolocation.getCurrentPosition(
        (pos) => {
          const loc = { lat: pos.coords.latitude, lng: pos.coords.longitude };
          setUserLocation(loc);
          setFlyToTarget(loc);
          toast.success("Đã định vị vị trí của bạn");
        },
        () => {
          toast.error("Không thể lấy vị trí GPS hiện tại");
        },
        { enableHighAccuracy: true, timeout: 8000 },
      );
    } else {
      toast.error("Trình duyệt không hỗ trợ GPS");
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
        () => {
          setUserLocation(DEFAULT_COORDS);
          setFlyToTarget(DEFAULT_COORDS);
        },
        { timeout: 8000 },
      );
    } else {
      setUserLocation(DEFAULT_COORDS);
      setFlyToTarget(DEFAULT_COORDS);
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
    setSelectedPlace(place);
    setFlyToTarget({ lat: place.latitude, lng: place.longitude });
  }

  function handleSelectPlaceFromModalOrChat(lat: number, lng: number, name: string) {
    setFlyToTarget({ lat, lng });
    const matched = places.find(
      (p) => Math.abs(p.latitude - lat) < 0.0001 && Math.abs(p.longitude - lng) < 0.0001,
    );
    if (matched) {
      setSelectedPlace(matched);
    } else {
      setSelectedPlace({
        id: `custom_${Date.now()}`,
        name,
        latitude: lat,
        longitude: lng,
      });
    }
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
      />

      {/* Top Search Bar & Controls */}
      <div className="absolute top-4 left-4 right-16 sm:right-20 mx-auto max-w-xl z-[990]">
        <SearchBar
          onSearch={handleSearch}
          loading={loading}
        />
      </div>

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
        className="fixed right-5 bottom-24 z-[1000] flex h-11 w-11 items-center justify-center rounded-full bg-card/95 border border-border shadow-md backdrop-blur-md text-foreground hover:bg-muted active:scale-95 transition-all"
        title="Vị trí của tôi (GPS)"
        aria-label="Vị trí của tôi (GPS)"
      >
        <LocateFixed className="h-5 w-5 text-foreground" />
      </button>

      {/* Bottom Place Card Detail */}
      {selectedPlace && (
        <PlaceCard
          place={selectedPlace}
          onClose={() => setSelectedPlace(null)}
          onScheduleMeeting={() => setIsMeetingModalOpen(true)}
        />
      )}

      {/* Fair Meeting & Schedule Modal */}
      {isMeetingModalOpen && (
        <FairMeetingModal
          userLocation={userLocation}
          selectedPlace={selectedPlace}
          onClose={() => setIsMeetingModalOpen(false)}
          onSelectPlaceOnMap={handleSelectPlaceFromModalOrChat}
        />
      )}

      {/* Conversational AI Chat Widget */}
      <AiChatWidget
        userLocation={userLocation}
        onSelectPlaceOnMap={handleSelectPlaceFromModalOrChat}
        onOpenMeetingModal={() => setIsMeetingModalOpen(true)}
      />
    </main>
  );
}