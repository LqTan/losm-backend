"use client";

import { isCurrentUserOrigin } from "@/lib/origin-labels";
import L from "leaflet";
import {
  CircleMarker,
  MapContainer,
  Marker,
  Polyline,
  Popup,
  TileLayer,
  Tooltip,
  useMap,
} from "react-leaflet";
import type { Place, MeetingPlace } from "@/types/place";
import { Fragment, useEffect } from "react";
import "leaflet/dist/leaflet.css";
import AppTileLayer from "@/components/map/AppTileLayer";

const DEFAULT_CENTER: [number, number] = [10.7769, 106.7009];
const ORIGIN_COLORS = ["#0284c7", "#f97316", "#a855f7", "#10b981", "#ef4444"];

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

function createTeardropPinIcon({
  name,
  avatarUrl,
  color = "#2563eb",
  isMe = false,
}: {
  name: string;
  avatarUrl?: string;
  color?: string;
  isMe?: boolean;
}) {
  const pinWidth = 40;
  const pinHeight = 52;
  const iconAnchorX = 20;
  const iconAnchorY = 50;

  const clipId = `av_clip_${Math.random().toString(36).slice(2, 9)}`;

  const safeUrl = avatarUrl && avatarUrl.trim()
    ? (avatarUrl.trim().startsWith("http")
        ? `/api/avatar?url=${encodeURIComponent(avatarUrl.trim())}`
        : avatarUrl.trim())
    : `/api/avatar?name=${encodeURIComponent(name || "Tôi")}&color=${encodeURIComponent(color)}`;

  const badgeBorderColor = isMe ? "#10b981" : "#ffffff";

  const html = `
    <div style="position: relative; width: ${pinWidth}px; height: ${pinHeight}px; display: flex; flex-direction: column; align-items: center; cursor: pointer; filter: drop-shadow(0 4px 6px rgba(0,0,0,0.45));">
      <svg width="${pinWidth}" height="${pinHeight}" viewBox="0 0 40 52" fill="none" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" style="position: absolute; left: 0; top: 0; pointer-events: none;">
        <defs>
          <clipPath id="${clipId}">
            <circle cx="20" cy="20" r="14.5"/>
          </clipPath>
        </defs>
        <path d="M20 2 C10.1 2 2 10.1 2 20 C2 31.5 20 50 20 50 C20 50 38 31.5 38 20 C38 10.1 29.9 2 20 2 Z" fill="${color}" stroke="${badgeBorderColor}" stroke-width="2.5" stroke-linejoin="round"/>
        <circle cx="20" cy="20" r="15" fill="#ffffff"/>
        <image href="${safeUrl}" xlink:href="${safeUrl}" x="5.5" y="5.5" width="29" height="29" preserveAspectRatio="xMidYMid slice" clip-path="url(#${clipId})"/>
      </svg>
      <div style="position: absolute; top: 5px; left: 5px; width: 30px; height: 30px; border-radius: 50%; overflow: hidden; z-index: 10; display: block; box-shadow: 0 1px 2px rgba(0,0,0,0.2);">
        <img
          src="${safeUrl}"
          alt="${name}"
          style="width: 30px !important; height: 30px !important; min-width: 30px !important; min-height: 30px !important; max-width: 30px !important; max-height: 30px !important; object-fit: cover; border-radius: 50%; display: block;"
        />
      </div>
      <span style="position: absolute; top: -14px; background: ${isMe ? '#10b981' : color}; color: white; font-size: 9px; font-weight: 800; padding: 1px 6px; border-radius: 9999px; white-space: nowrap; box-shadow: 0 2px 4px rgba(0,0,0,0.3); border: 1px solid white; z-index: 20; max-width: 85px; overflow: hidden; text-overflow: ellipsis;">
        ${name || (isMe ? "Tôi" : "Người")}
      </span>
    </div>
  `;

  return L.divIcon({
    className: "losm-teardrop-pin",
    html,
    iconSize: [pinWidth, pinHeight],
    iconAnchor: [iconAnchorX, iconAnchorY],
    popupAnchor: [0, -iconAnchorY],
  });
}

export interface MapOrigin {
  id?: string;
  name: string;
  lat: number;
  lng: number;
  avatarUrl?: string;
  isMe?: boolean;
}

interface Props {
  places: Place[];
  selectedPlace: Place | null;
  userLocation: { lat: number; lng: number } | null;
  onSelectPlace: (place: Place) => void;
  flyToTarget?: { lat: number; lng: number } | null;
  groupOrigins?: MapOrigin[];
  centroid?: { lat: number; lng: number } | null;
  activeMeetingPlace?: MeetingPlace | null;
  currentUserAvatar?: string;
  navigationRoute?: {
    coordinates: [number, number][];
    distanceKm: number;
    durationMinutes: number;
  } | null;
}

function RouteBoundsController({
  route,
}: {
  route?: { coordinates: [number, number][] } | null;
}) {
  const map = useMap();

  useEffect(() => {
    if (route && route.coordinates && route.coordinates.length > 1) {
      const bounds = L.latLngBounds(route.coordinates);
      map.fitBounds(bounds, {
        padding: [80, 80],
        maxZoom: 16,
      });
    }
  }, [route, map]);

  return null;
}

function GroupMeetingBoundsController({
  groupOrigins,
  activeMeetingPlace,
  navigationRoute,
}: {
  groupOrigins?: MapOrigin[];
  activeMeetingPlace?: MeetingPlace | null;
  navigationRoute?: { coordinates: [number, number][] } | null;
}) {
  const map = useMap();

  useEffect(() => {
    if (navigationRoute && navigationRoute.coordinates && navigationRoute.coordinates.length > 1) {
      return;
    }

    if (groupOrigins && groupOrigins.length > 1) {
      const points: [number, number][] = groupOrigins.map((o) => [o.lat, o.lng]);
      if (activeMeetingPlace) {
        points.push([activeMeetingPlace.latitude, activeMeetingPlace.longitude]);
      }
      if (points.length > 0) {
        const bounds = L.latLngBounds(points);
        const isSmallScreen = typeof window !== "undefined" && window.innerHeight < 650;
        const bottomPadding = isSmallScreen ? 140 : 220;
        map.fitBounds(bounds, {
          paddingTopLeft: [70, 70],
          paddingBottomRight: [70, bottomPadding],
          maxZoom: 16,
        });
      }
    }
  }, [groupOrigins, activeMeetingPlace, navigationRoute, map]);

  return null;
}

function MapViewController({
  target,
  disabled,
}: {
  target?: { lat: number; lng: number } | null;
  disabled?: boolean;
}) {
  const map = useMap();

  useEffect(() => {
    if (disabled) return;
    if (target && target.lat && target.lng) {
      map.flyTo([target.lat, target.lng], 16, {
        duration: 1.2,
      });
    }
  }, [target?.lat, target?.lng, disabled, map]);

  return null;
}

export default function MapView({
  places,
  selectedPlace,
  userLocation,
  onSelectPlace,
  flyToTarget,
  groupOrigins,
  centroid,
  activeMeetingPlace,
  currentUserAvatar,
  navigationRoute,
}: Props) {
  const centerPos = userLocation
    ? ([userLocation.lat, userLocation.lng] as [number, number])
    : DEFAULT_CENTER;

  const isGroupMode = Boolean(groupOrigins && groupOrigins.length > 1);
  const activeTarget = flyToTarget ?? (selectedPlace ? { lat: selectedPlace.latitude, lng: selectedPlace.longitude } : null);

  return (
    <MapContainer
      center={centerPos}
      zoom={14}
      zoomControl={false}
      className="h-full w-full"
    >
      <AppTileLayer />
      <MapViewController
        target={navigationRoute ? null : activeTarget}
        disabled={Boolean(navigationRoute || isGroupMode)}
      />
      <GroupMeetingBoundsController
        groupOrigins={groupOrigins}
        activeMeetingPlace={activeMeetingPlace}
        navigationRoute={navigationRoute}
      />
      <RouteBoundsController route={navigationRoute} />

      {/* User Current Location Marker */}
      {userLocation && (!groupOrigins || groupOrigins.length === 0 || !groupOrigins.some((o) => o.isMe)) && (
        <Fragment key={`user_loc_group_${userLocation.lat}_${userLocation.lng}`}>
          <CircleMarker
            center={[userLocation.lat, userLocation.lng]}
            radius={7}
            pathOptions={{
              color: "#ffffff",
              weight: 2,
              fillColor: "#0284c7",
              fillOpacity: 0.9,
            }}
          >
            <Popup>
              <div className="text-xs font-semibold p-1">
                📍 Vị trí hiện tại của bạn
              </div>
            </Popup>
          </CircleMarker>
          {(!groupOrigins || groupOrigins.length === 0) && (
            <Marker
              key={`user_pin_${userLocation.lat}_${userLocation.lng}_${currentUserAvatar || "no_av"}`}
              position={[userLocation.lat, userLocation.lng]}
              icon={createTeardropPinIcon({
                name: "Vị trí của bạn",
                avatarUrl: currentUserAvatar,
                color: "#0284c7",
                isMe: true,
              })}
            >
              <Popup>
                <div className="p-1 text-xs font-bold text-slate-900">
                  <div className="flex items-center gap-1.5 mb-0.5">
                    {currentUserAvatar ? (
                      <img
                        src={`/api/avatar?url=${encodeURIComponent(currentUserAvatar)}`}
                        alt="Tôi"
                        className="h-5 w-5 rounded-full object-cover border"
                      />
                    ) : (
                      <span>📍</span>
                    )}
                    <span>Vị trí hiện tại của bạn</span>
                  </div>
                  <div className="text-[10px] text-slate-500 font-normal">
                    {userLocation.lat.toFixed(4)}, {userLocation.lng.toFixed(4)}
                  </div>
                </div>
              </Popup>
            </Marker>
          )}
        </Fragment>
      )}

      {/* Group Origins Markers */}
      {groupOrigins &&
        groupOrigins.map((orig, idx) => {
          const color = ORIGIN_COLORS[idx % ORIGIN_COLORS.length];
          const isMe = isCurrentUserOrigin(orig);
          const avatar = isMe ? (orig.avatarUrl || currentUserAvatar) : orig.avatarUrl;

          return (
            <Fragment key={`orig_marker_grp_${idx}_${orig.lat}_${orig.lng}`}>
              {/* Giữ nguyên chấm tròn định vị ("bổ sung thêm không có bảo sửa hay xóa nha") */}
              <CircleMarker
                key={`orig_dot_${idx}_${orig.lat}_${orig.lng}`}
                center={[orig.lat, orig.lng]}
                radius={6}
                pathOptions={{
                  color: "#ffffff",
                  weight: 2,
                  fillColor: color,
                  fillOpacity: 0.95,
                }}
              />
              {/* Bổ sung Marker hình giọt nước úp ngược với Avatar tròn bên trong */}
              <Marker
                key={`orig_pin_${idx}_${orig.lat}_${orig.lng}_${avatar || "no_av"}`}
                position={[orig.lat, orig.lng]}
                icon={createTeardropPinIcon({
                  name: orig.name,
                  avatarUrl: avatar,
                  color: color,
                  isMe: isMe,
                })}
              >
                <Popup>
                  <div className="p-1 text-xs font-bold text-slate-900">
                    <div className="flex items-center gap-1.5 mb-1">
                      {avatar ? (
                        <img
                          src={avatar.startsWith("http") ? `/api/avatar?url=${encodeURIComponent(avatar)}` : avatar}
                          alt={orig.name}
                          className="h-5 w-5 rounded-full object-cover border"
                        />
                      ) : (
                        <span>👤</span>
                      )}
                      <span>{orig.name}</span>
                      {isMe && (
                        <span className="text-[10px] bg-emerald-100 text-emerald-700 px-1.5 py-0.5 rounded font-semibold">
                          Chủ trì
                        </span>
                      )}
                    </div>
                    <div className="text-[10px] text-slate-500 font-normal">
                      Tọa độ: {orig.lat.toFixed(4)}, {orig.lng.toFixed(4)}
                    </div>
                  </div>
                </Popup>
              </Marker>
            </Fragment>
          );
        })}

      {/* Centroid Marker (Green crosshair) */}
      {centroid && (
        <CircleMarker
          center={[centroid.lat, centroid.lng]}
          radius={11}
          pathOptions={{
            color: "#059669",
            weight: 2,
            dashArray: "4, 4",
            fillColor: "#10b981",
            fillOpacity: 0.35,
          }}
        >
          <Popup>
            <div className="p-1 text-xs font-bold text-emerald-800">
              🎯 Tâm điểm hẹn lý tưởng (Centroid)
            </div>
          </Popup>
        </CircleMarker>
      )}

      {/* Polyline connections to active meeting place */}
      {activeMeetingPlace &&
        groupOrigins &&
        groupOrigins.map((orig, idx) => {
          const distNum =
            activeMeetingPlace.distanceKmByOrigin?.[idx] ??
            getDistanceKm(orig.lat, orig.lng, activeMeetingPlace.latitude, activeMeetingPlace.longitude);
          const dur = activeMeetingPlace.durationMinutesByOrigin?.[idx];

          return (
            <Polyline
              key={`conn_line_${idx}_${activeMeetingPlace.latitude}_${activeMeetingPlace.longitude}`}
              positions={[
                [orig.lat, orig.lng],
                [activeMeetingPlace.latitude, activeMeetingPlace.longitude],
              ]}
              pathOptions={{
                color: "#2563eb",
                weight: 3,
                dashArray: "6, 6",
                opacity: 0.85,
              }}
            >
              <Tooltip
                permanent
                direction="center"
                className="distance-badge"
              >
                <div className="flex items-center gap-1.5 font-bold text-[11px] bg-slate-900/95 text-white border border-slate-700 shadow-xl px-2.5 py-1 rounded-full whitespace-nowrap backdrop-blur-md">
                  <span className="text-blue-400 font-semibold">{orig.name}:</span>
                  <span>{distNum.toFixed(1)} km</span>
                  {dur !== undefined && dur !== null && (
                    <span className="text-slate-300 text-[10px] font-normal">
                      (~{Math.round(dur)}p)
                    </span>
                  )}
                </div>
              </Tooltip>

              <Popup>
                <div className="p-1 text-xs font-medium space-y-0.5">
                  <div className="font-bold text-slate-900">
                    🚗 {orig.name} ➔ {activeMeetingPlace.name}
                  </div>
                  <div className="text-blue-600 font-semibold">
                    Khoảng cách: {distNum.toFixed(1)} km
                  </div>
                  {dur !== undefined && dur !== null && (
                    <div className="text-slate-600">
                      Thời gian ước tính: ~{Math.round(dur)} phút
                    </div>
                  )}
                </div>
              </Popup>
            </Polyline>
          );
        })}

      {/* Place Markers */}
      {places.map((place) => {
        const isActiveMeeting =
          activeMeetingPlace &&
          (activeMeetingPlace.placeId === String(place.id) ||
            (Math.abs(activeMeetingPlace.latitude - place.latitude) < 0.0001 &&
              Math.abs(activeMeetingPlace.longitude - place.longitude) < 0.0001));
        const isSelected = selectedPlace?.id === place.id || Boolean(isActiveMeeting);
        const color = isActiveMeeting ? "#8b5cf6" : isSelected ? "#ef4444" : "#2563eb";

        return (
          <Fragment key={place.id}>
            {isSelected && (
              <CircleMarker
                center={[place.latitude, place.longitude]}
                radius={18}
                pathOptions={{
                  color: "#ef4444",
                  weight: 2,
                  fillColor: "#ef4444",
                  fillOpacity: 0.25,
                }}
              />
            )}
            <CircleMarker
              center={[place.latitude, place.longitude]}
              radius={isActiveMeeting ? 14 : isSelected ? 12 : 8}
              pathOptions={{
                color: isSelected ? "#ffffff" : color,
                weight: isSelected ? 3 : 1.5,
                fillColor: color,
                fillOpacity: isSelected ? 1 : 0.85,
              }}
              eventHandlers={{
                click: () => onSelectPlace(place),
              }}
            >
              <Popup>
                <div className="p-1 space-y-1 text-xs">
                  {isActiveMeeting && (
                    <div className="inline-block bg-purple-100 text-purple-700 font-bold px-1.5 py-0.5 rounded text-[10px] mb-0.5">
                      📍 Điểm hẹn đang chọn
                    </div>
                  )}
                  <p className="font-bold text-sm text-gray-900 leading-tight">
                    {place.name}
                  </p>
                  {place.category && (
                    <p className="text-gray-500 font-medium">
                      {place.category}
                    </p>
                  )}
                  {place.address && (
                    <p className="text-gray-600 line-clamp-2">
                      {place.address}
                    </p>
                  )}
                  {place.distanceKm !== undefined && (
                    <p className="text-blue-600 font-semibold pt-0.5">
                      Cách bạn: {place.distanceKm.toFixed(1)} km
                    </p>
                  )}
                  <button
                    type="button"
                    onClick={() => onSelectPlace(place)}
                    className="mt-1 w-full rounded-md bg-blue-600 py-1 text-[11px] font-semibold text-white hover:bg-blue-700 transition"
                  >
                    Xem chi tiết
                  </button>
                </div>
              </Popup>
            </CircleMarker>
          </Fragment>
        );
      })}

      {/* Standalone Marker for selectedPlace if not in places array */}
      {selectedPlace &&
        !places.some(
          (p) =>
            p.id === selectedPlace.id ||
            (Math.abs(p.latitude - selectedPlace.latitude) < 0.0001 &&
              Math.abs(p.longitude - selectedPlace.longitude) < 0.0001)
        ) && (
          <Fragment key={`standalone_frag_${selectedPlace.id}`}>
            <CircleMarker
              center={[selectedPlace.latitude, selectedPlace.longitude]}
              radius={18}
              pathOptions={{
                color: "#ef4444",
                weight: 2,
                fillColor: "#ef4444",
                fillOpacity: 0.25,
              }}
            />
            <CircleMarker
              center={[selectedPlace.latitude, selectedPlace.longitude]}
              radius={12}
              pathOptions={{
                color: "#ffffff",
                weight: 3,
                fillColor: "#ef4444",
                fillOpacity: 1,
              }}
              eventHandlers={{
                click: () => onSelectPlace(selectedPlace),
              }}
            >
            <Popup>
              <div className="p-1 space-y-1 text-xs">
                <p className="font-bold text-sm text-gray-900 leading-tight">
                  {selectedPlace.name}
                </p>
                {selectedPlace.category && (
                  <p className="text-gray-500 font-medium">
                    {selectedPlace.category}
                  </p>
                )}
                {selectedPlace.address && (
                  <p className="text-gray-600 line-clamp-2">
                    {selectedPlace.address}
                  </p>
                )}
                {selectedPlace.distanceKm !== undefined && (
                  <p className="text-blue-600 font-semibold pt-0.5">
                    Cách bạn: {selectedPlace.distanceKm.toFixed(1)} km
                  </p>
                )}
              </div>
            </Popup>
          </CircleMarker>
        </Fragment>
      )}

      {/* Driving Navigation Route from OSRM */}
      {navigationRoute && navigationRoute.coordinates && navigationRoute.coordinates.length > 0 && (
        <>
          <Polyline
            positions={navigationRoute.coordinates}
            pathOptions={{
              color: "#60a5fa",
              weight: 8,
              opacity: 0.45,
              lineJoin: "round",
            }}
          />
          <Polyline
            positions={navigationRoute.coordinates}
            pathOptions={{
              color: "#2563eb",
              weight: 4.5,
              opacity: 0.95,
              lineJoin: "round",
            }}
          />
        </>
      )}
    </MapContainer>
  );
}
