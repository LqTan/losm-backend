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
import type { TransitRouteDetails } from "../ai/AiChatWidget";
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

function createTransitPinIcon({
  color,
  badgeText,
  iconSvg,
}: {
  color: string;
  badgeText: string;
  iconSvg: string;
}) {
  const pinWidth = 36;
  const pinHeight = 48;
  const iconAnchorX = 18;
  const iconAnchorY = 46;

  const html = `
    <div style="position: relative; width: ${pinWidth}px; height: ${pinHeight}px; display: flex; flex-direction: column; align-items: center; cursor: pointer; filter: drop-shadow(0 4px 6px rgba(0,0,0,0.35));">
      <svg width="${pinWidth}" height="${pinHeight}" viewBox="0 0 36 48" fill="none" xmlns="http://www.w3.org/2000/svg" style="position: absolute; left: 0; top: 0; pointer-events: none;">
        <path d="M18 2 C9.2 2 2 9.2 2 18 C2 29 18 46 18 46 C18 46 34 29 34 18 C34 9.2 26.8 2 18 2 Z" fill="${color}" stroke="#ffffff" stroke-width="2.5" stroke-linejoin="round"/>
        <circle cx="18" cy="18" r="11" fill="#ffffff"/>
      </svg>
      <div style="position: absolute; top: 7px; left: 7px; width: 22px; height: 22px; border-radius: 50%; display: flex; align-items: center; justify-content: center; z-index: 10;">
        ${iconSvg}
      </div>
      <span style="position: absolute; top: -14px; background: ${color}; color: white; font-size: 9px; font-weight: 700; padding: 1px 6px; border-radius: 9999px; white-space: nowrap; box-shadow: 0 2px 4px rgba(0,0,0,0.25); border: 1px solid white; z-index: 20; max-width: 120px; overflow: hidden; text-overflow: ellipsis;">
        ${badgeText}
      </span>
    </div>
  `;

  return L.divIcon({
    className: "losm-transit-pin",
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
    transitDetails?: TransitRouteDetails;
  } | null;
}

function RouteBoundsController({
  route,
}: {
  route?: {
    coordinates: [number, number][];
    transitDetails?: TransitRouteDetails;
  } | null;
}) {
  const map = useMap();

  useEffect(() => {
    if (!route) return;

    const allPoints: [number, number][] = [];

    if (route.transitDetails) {
      const td = route.transitDetails;
      if (td.origin && td.origin.latitude && td.origin.longitude) {
        allPoints.push([td.origin.latitude, td.origin.longitude]);
      }
      if (td.destination && td.destination.latitude && td.destination.longitude) {
        allPoints.push([td.destination.latitude, td.destination.longitude]);
      }
      if (td.boardingStop && td.boardingStop.latitude && td.boardingStop.longitude) {
        allPoints.push([td.boardingStop.latitude, td.boardingStop.longitude]);
      }
      if (td.alightingStop && td.alightingStop.latitude && td.alightingStop.longitude) {
        allPoints.push([td.alightingStop.latitude, td.alightingStop.longitude]);
      }
      if (td.intermediateStops && td.intermediateStops.length > 0) {
        td.intermediateStops.forEach((s) => {
          if (s.latitude && s.longitude) allPoints.push([s.latitude, s.longitude]);
        });
      }
      if (td.walkOriginToBoarding) {
        allPoints.push(...td.walkOriginToBoarding);
      }
      if (td.transitCoordinates) {
        allPoints.push(...td.transitCoordinates);
      }
      if (td.walkAlightingToDest) {
        allPoints.push(...td.walkAlightingToDest);
      }
    } else if (route.coordinates && route.coordinates.length > 0) {
      allPoints.push(...route.coordinates);
    }

    if (allPoints.length > 1) {
      const bounds = L.latLngBounds(allPoints);
      map.fitBounds(bounds, {
        padding: [80, 80],
        maxZoom: 16,
      });
    } else if (allPoints.length === 1) {
      map.flyTo(allPoints[0], 16);
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

      {/* Standalone Marker for selectedPlace if not in places array and no transit route active */}
      {selectedPlace &&
        !navigationRoute?.transitDetails &&
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

      {/* Transit Route Visualization (Walking dashed lines + Solid bus lines + Markers) */}
      {navigationRoute?.transitDetails ? (
        <>
          {/* 1. Walk from origin to boarding stop (nét đứt đi bộ) */}
          {navigationRoute.transitDetails.walkOriginToBoarding &&
            navigationRoute.transitDetails.walkOriginToBoarding.length > 1 && (
              <>
                <Polyline
                  positions={navigationRoute.transitDetails.walkOriginToBoarding}
                  pathOptions={{
                    color: "#fdba74",
                    weight: 6,
                    opacity: 0.5,
                    dashArray: "6, 8",
                    lineCap: "round",
                  }}
                />
                <Polyline
                  positions={navigationRoute.transitDetails.walkOriginToBoarding}
                  pathOptions={{
                    color: "#ea580c",
                    weight: 3.5,
                    opacity: 0.95,
                    dashArray: "6, 8",
                    lineCap: "round",
                  }}
                />
              </>
            )}

          {/* 2. Bus ride solid route */}
          {navigationRoute.transitDetails.transitCoordinates &&
            navigationRoute.transitDetails.transitCoordinates.length > 0 && (
              <>
                <Polyline
                  positions={navigationRoute.transitDetails.transitCoordinates}
                  pathOptions={{
                    color: "#60a5fa",
                    weight: 9,
                    opacity: 0.45,
                    lineJoin: "round",
                  }}
                />
                <Polyline
                  positions={navigationRoute.transitDetails.transitCoordinates}
                  pathOptions={{
                    color: "#2563eb",
                    weight: 5,
                    opacity: 0.95,
                    lineJoin: "round",
                  }}
                />
              </>
            )}

          {/* 3. Walk from alighting stop to destination (nét đứt đi bộ) */}
          {navigationRoute.transitDetails.walkAlightingToDest &&
            navigationRoute.transitDetails.walkAlightingToDest.length > 1 && (
              <>
                <Polyline
                  positions={navigationRoute.transitDetails.walkAlightingToDest}
                  pathOptions={{
                    color: "#fdba74",
                    weight: 6,
                    opacity: 0.5,
                    dashArray: "6, 8",
                    lineCap: "round",
                  }}
                />
                <Polyline
                  positions={navigationRoute.transitDetails.walkAlightingToDest}
                  pathOptions={{
                    color: "#ea580c",
                    weight: 3.5,
                    opacity: 0.95,
                    dashArray: "6, 8",
                    lineCap: "round",
                  }}
                />
              </>
            )}

          {/* Transit Markers */}
          {(() => {
            const td = navigationRoute.transitDetails;
            if (!td) return null;
            const origin = td.origin;
            const boarding = td.boardingStop;
            const alighting = td.alightingStop;
            const dest = td.destination;

            const isAtBoardingStop =
              !origin?.latitude ||
              !origin?.longitude ||
              Math.hypot(
                (origin.latitude - boarding.latitude) * 111320,
                (origin.longitude - boarding.longitude) *
                  111320 *
                  Math.cos((boarding.latitude * Math.PI) / 180)
              ) < 35;

            const isAtAlightingStop =
              !dest?.latitude ||
              !dest?.longitude ||
              Math.hypot(
                (dest.latitude - alighting.latitude) * 111320,
                (dest.longitude - alighting.longitude) *
                  111320 *
                  Math.cos((alighting.latitude * Math.PI) / 180)
              ) < 35;

            return (
              <>
                {/* 4 & 5: Start / Boarding Stop Markers */}
                {isAtBoardingStop ? (
                  <Marker
                    position={[boarding.latitude, boarding.longitude]}
                    icon={createTransitPinIcon({
                      color: "#2563eb",
                      badgeText: "Xuất phát & Lên xe",
                      iconSvg: `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#2563eb" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M8 6v6"/><path d="M15 6v6"/><path d="M2 12h19.6"/><path d="M18 18h3s.5-1.7.8-2.8c.1-.4.2-.8.2-1.2 0-.4-.1-.8-.2-1.2l-1.4-5C20.1 6.8 19.1 6 18 6H4a2 2 0 0 0-2 2v10h3"/><circle cx="7" cy="18" r="2"/><path d="M9 18h5"/><circle cx="16" cy="18" r="2"/></svg>`,
                    })}
                  >
                    <Tooltip direction="top" offset={[0, -28]} opacity={0.95}>
                      <span className="font-bold text-xs text-blue-700">
                        Vị trí bạn đứng & Lên xe (Tuyến {td.routeNumber || ""}): {boarding.name}
                      </span>
                    </Tooltip>
                    <Popup>
                      <div className="space-y-1 text-xs">
                        <p className="font-bold text-blue-600 flex items-center gap-1">
                          <span className="h-2 w-2 rounded-full bg-blue-500 inline-block" />
                          Vị trí bạn đứng & Lên xe (Tuyến {td.routeNumber || ""})
                        </p>
                        <p className="font-semibold text-gray-900">
                          {origin?.name && origin.name !== "Điểm xuất phát"
                            ? origin.name
                            : boarding.name}
                        </p>
                        <p className="text-emerald-600 font-medium text-[11px]">
                          Bạn đang ở ngay tại trạm xe buýt. Đón xe tại đây!
                        </p>
                        {boarding.address && (
                          <p className="text-gray-500 text-[11px]">{boarding.address}</p>
                        )}
                      </div>
                    </Popup>
                  </Marker>
                ) : (
                  <>
                    {origin?.latitude && origin?.longitude && (
                      <Marker
                        position={[origin.latitude, origin.longitude]}
                        icon={createTransitPinIcon({
                          color: "#10b981",
                          badgeText: "Xuất phát",
                          iconSvg: `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#10b981" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="7" r="4"/><path d="M5.5 21v-2a7 7 0 0 1 13 0v2"/></svg>`,
                        })}
                      >
                        <Tooltip direction="top" offset={[0, -28]} opacity={0.95}>
                          <span className="font-bold text-xs text-emerald-700">
                            Điểm xuất phát: {origin.name}
                          </span>
                        </Tooltip>
                        <Popup>
                          <div className="space-y-1 text-xs">
                            <p className="font-bold text-emerald-600 flex items-center gap-1">
                              <span className="h-2 w-2 rounded-full bg-emerald-500 inline-block" />
                              Điểm xuất phát
                            </p>
                            <p className="font-semibold text-gray-900">{origin.name}</p>
                            <p className="text-gray-500 text-[11px]">
                              Đi bộ đến trạm đón xe buýt
                            </p>
                          </div>
                        </Popup>
                      </Marker>
                    )}
                    <Marker
                      position={[boarding.latitude, boarding.longitude]}
                      icon={createTransitPinIcon({
                        color: "#2563eb",
                        badgeText: "Trạm lên xe",
                        iconSvg: `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#2563eb" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M8 6v6"/><path d="M15 6v6"/><path d="M2 12h19.6"/><path d="M18 18h3s.5-1.7.8-2.8c.1-.4.2-.8.2-1.2 0-.4-.1-.8-.2-1.2l-1.4-5C20.1 6.8 19.1 6 18 6H4a2 2 0 0 0-2 2v10h3"/><circle cx="7" cy="18" r="2"/><path d="M9 18h5"/><circle cx="16" cy="18" r="2"/></svg>`,
                      })}
                    >
                      <Tooltip direction="top" offset={[0, -28]} opacity={0.95}>
                        <span className="font-bold text-xs text-blue-700">
                          Trạm lên xe: {boarding.name} (Tuyến {td.routeNumber || ""})
                        </span>
                      </Tooltip>
                      <Popup>
                        <div className="space-y-1 text-xs">
                          <p className="font-bold text-blue-600 flex items-center gap-1">
                            <span className="h-2 w-2 rounded-full bg-blue-500 inline-block" />
                            Trạm lên xe (Tuyến {td.routeNumber || ""})
                          </p>
                          <p className="font-semibold text-gray-900">{boarding.name}</p>
                          {boarding.address && (
                            <p className="text-gray-500 text-[11px]">{boarding.address}</p>
                          )}
                        </div>
                      </Popup>
                    </Marker>
                  </>
                )}

                {/* 6. Intermediate Stops (dots with sequence and hover tooltip) */}
                {td.intermediateStops?.map((s, idx) => (
                  <CircleMarker
                    key={`${s.id}-${idx}`}
                    center={[s.latitude, s.longitude]}
                    radius={5.5}
                    pathOptions={{
                      color: "#1d4ed8",
                      fillColor: "#ffffff",
                      fillOpacity: 1,
                      weight: 2.5,
                    }}
                  >
                    <Tooltip direction="top" offset={[0, -6]} opacity={0.95}>
                      <span className="font-bold text-xs text-blue-700">
                        Trạm {idx + 1}: {s.name}
                      </span>
                    </Tooltip>
                    <Popup>
                      <div className="text-xs space-y-1">
                        <p className="font-bold text-blue-600 flex items-center gap-1">
                          <span className="h-2 w-2 rounded-full bg-blue-600 inline-block" />
                          Trạm trung gian #{idx + 1}
                        </p>
                        <p className="font-semibold text-gray-900">{s.name}</p>
                        <p className="text-gray-500 text-[11px]">
                          Tuyến {td.routeNumber || ""} · {td.routeName || ""}
                        </p>
                      </div>
                    </Popup>
                  </CircleMarker>
                ))}

                {/* 7 & 8: Alighting / Destination Markers */}
                {isAtAlightingStop ? (
                  <Marker
                    position={[alighting.latitude, alighting.longitude]}
                    icon={createTransitPinIcon({
                      color: "#f59e0b",
                      badgeText: "Xuống & Đến nơi",
                      iconSvg: `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#f59e0b" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M8 6v6"/><path d="M15 6v6"/><path d="M2 12h19.6"/><path d="M18 18h3s.5-1.7.8-2.8c.1-.4.2-.8.2-1.2 0-.4-.1-.8-.2-1.2l-1.4-5C20.1 6.8 19.1 6 18 6H4a2 2 0 0 0-2 2v10h3"/><circle cx="7" cy="18" r="2"/><path d="M9 18h5"/><circle cx="16" cy="18" r="2"/></svg>`,
                    })}
                  >
                    <Tooltip direction="top" offset={[0, -28]} opacity={0.95}>
                      <span className="font-bold text-xs text-amber-700">
                        Trạm xuống & Điểm đến: {dest?.name || alighting.name}
                      </span>
                    </Tooltip>
                    <Popup>
                      <div className="space-y-1 text-xs">
                        <p className="font-bold text-amber-600 flex items-center gap-1">
                          <span className="h-2 w-2 rounded-full bg-amber-500 inline-block" />
                          Trạm xuống & Điểm đến
                        </p>
                        <p className="font-semibold text-gray-900">
                          {dest?.name || alighting.name}
                        </p>
                        {alighting.address && (
                          <p className="text-gray-500 text-[11px]">{alighting.address}</p>
                        )}
                      </div>
                    </Popup>
                  </Marker>
                ) : (
                  <>
                    <Marker
                      position={[alighting.latitude, alighting.longitude]}
                      icon={createTransitPinIcon({
                        color: "#f59e0b",
                        badgeText: "Trạm xuống",
                        iconSvg: `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#f59e0b" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><path d="M8 6v6"/><path d="M15 6v6"/><path d="M2 12h19.6"/><path d="M18 18h3s.5-1.7.8-2.8c.1-.4.2-.8.2-1.2 0-.4-.1-.8-.2-1.2l-1.4-5C20.1 6.8 19.1 6 18 6H4a2 2 0 0 0-2 2v10h3"/><circle cx="7" cy="18" r="2"/><path d="M9 18h5"/><circle cx="16" cy="18" r="2"/></svg>`,
                      })}
                    >
                      <Tooltip direction="top" offset={[0, -28]} opacity={0.95}>
                        <span className="font-bold text-xs text-amber-700">
                          Trạm xuống xe: {alighting.name}
                        </span>
                      </Tooltip>
                      <Popup>
                        <div className="space-y-1 text-xs">
                          <p className="font-bold text-amber-600 flex items-center gap-1">
                            <span className="h-2 w-2 rounded-full bg-amber-500 inline-block" />
                            Trạm xuống xe
                          </p>
                          <p className="font-semibold text-gray-900">{alighting.name}</p>
                          {alighting.address && (
                            <p className="text-gray-500 text-[11px]">{alighting.address}</p>
                          )}
                        </div>
                      </Popup>
                    </Marker>

                    {dest?.latitude && dest?.longitude && (
                      <Marker
                        position={[dest.latitude, dest.longitude]}
                        icon={createTransitPinIcon({
                          color: "#ef4444",
                          badgeText: "Điểm đến",
                          iconSvg: `<svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="#ef4444" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round"><circle cx="12" cy="12" r="10"/><circle cx="12" cy="12" r="6"/><circle cx="12" cy="12" r="2"/></svg>`,
                        })}
                      >
                        <Tooltip direction="top" offset={[0, -28]} opacity={0.95}>
                          <span className="font-bold text-xs text-rose-700">
                            Điểm đến: {dest.name}
                          </span>
                        </Tooltip>
                        <Popup>
                          <div className="space-y-1 text-xs">
                            <p className="font-bold text-rose-600 flex items-center gap-1">
                              <span className="h-2 w-2 rounded-full bg-rose-500 inline-block" />
                              Điểm đến
                            </p>
                            <p className="font-semibold text-gray-900">{dest.name}</p>
                          </div>
                        </Popup>
                      </Marker>
                    )}
                  </>
                )}
              </>
            );
          })()}
        </>
      ) : (
        /* Driving Navigation Route from OSRM */
        navigationRoute &&
        navigationRoute.coordinates &&
        navigationRoute.coordinates.length > 0 && (
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
        )
      )}
    </MapContainer>
  );
}
