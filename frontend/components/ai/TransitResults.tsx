"use client";

import { Bus, Calendar, Compass, Cpu, Sparkles, TrainFront } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { formatRouteNumber, formatServiceTime } from "@/lib/transit";
import type {
  TransitChatJourney,
  TransitChatLocation,
  TransitChatResult,
  TransitChatRoute,
  TransitChatStop,
} from "@/lib/transit-chat";

type Props = {
  results: TransitChatResult[];
  onSelectStop?: (stop: TransitChatStop) => void;
  onSelectJourney?: (
    journey: TransitChatJourney,
    origin?: TransitChatLocation | null,
    destination?: TransitChatLocation | null
  ) => void;
  onScheduleMeeting?: (place: {
    id: string;
    name: string;
    address?: string;
    latitude: number;
    longitude: number;
  }) => void;
};

function RouteInfo({ route }: { route: TransitChatRoute }) {
  return (
    <div className="space-y-1">
      <p className="flex items-center gap-1 font-semibold text-foreground">
        {route.mode === "Metro" ? <TrainFront className="h-3.5 w-3.5" /> : <Bus className="h-3.5 w-3.5" />}
        Tuyến {formatRouteNumber(route.number)} · {route.name}
      </p>
      <p className="text-muted-foreground">{route.directionName}</p>
      <p className="text-muted-foreground">
        Giờ đầu/cuối tại đầu tuyến: {formatServiceTime(route.firstMinute)} – {formatServiceTime(route.lastMinute)}
      </p>
      {route.headwayMin != null && route.headwayMax != null && (
        <p className="text-muted-foreground">Giãn chuyến: {route.headwayMin}–{route.headwayMax} phút</p>
      )}
      <p className="text-muted-foreground">
        Lịch áp dụng: {({ Daily: "Hằng ngày", Weekdays: "Ngày trong tuần", Weekends: "Cuối tuần" } as Record<string, string>)[route.serviceDays] ?? "Chưa có dữ liệu"}
      </p>
      {route.referenceFare != null && (
        <p className="text-muted-foreground">
          Vé tham khảo: {route.referenceFare.toLocaleString("vi-VN")} VNĐ{route.fareDescription ? ` · ${route.fareDescription}` : ""}
        </p>
      )}
    </div>
  );
}

export default function TransitResults({
  results,
  onSelectStop,
  onSelectJourney,
  onScheduleMeeting,
}: Props) {
  // 1. Gom tất cả các hành trình lộ trình (journeys) từ mọi kết quả
  const allJourneyItems = results.flatMap((r) =>
    r.journeys.map((j) => ({ journey: j, origin: r.origin, destination: r.destination }))
  );
  const hasJourneys = allJourneyItems.length > 0;

  // 2. Gom các trạm dừng độc lập
  const seenStopIds = new Set<string | number>();
  const allStops = results
    .flatMap((r) => r.stops.map((s) => ({ stop: s, origin: r.origin, destination: r.destination })))
    .filter(({ stop }) => {
      if (seenStopIds.has(stop.id)) return false;
      seenStopIds.add(stop.id);
      return true;
    });

  // 3. Gom các tuyến xe
  const seenPatternIds = new Set<string>();
  const allRoutes = results
    .flatMap((r) => r.routes)
    .filter((route) => {
      if (seenPatternIds.has(route.patternId)) return false;
      seenPatternIds.add(route.patternId);
      return true;
    });

  const statuses = Array.from(new Set(results.map((r) => r.status).filter(Boolean)));
  const supportedAreas = Array.from(new Set(results.flatMap((r) => r.supportedAreas)));

  return (
    <div className="mt-3 space-y-3 text-xs">
      {/* 1. KHI CÓ HÀNH TRÌNH TÌM ĐƯỜNG: CHỈ HIỂN THỊ CÁC THẺ HÀNH TRÌNH ĐỒNG BỘ */}
      {hasJourneys &&
        allJourneyItems.map(({ journey, origin, destination }, idx) => (
          <div
            key={`journey-${journey.route.patternId}-${journey.boardingStop.id}-${journey.alightingStop.id}-${idx}`}
            className="space-y-2 rounded-xl border border-blue-500/35 bg-blue-500/5 p-3 shadow-xs"
          >
            <div className="flex items-center justify-between gap-1.5 flex-wrap pb-1 border-b border-blue-500/15">
              <Badge
                variant="outline"
                className="text-[9px] px-1.5 py-0 h-4.5 font-medium bg-blue-500/10 text-blue-700 dark:text-blue-300 border-blue-300/40 flex items-center gap-1 shadow-2xs"
                title="Hành trình được tối ưu hoá bằng Thuật toán đa tiêu chí CRADIS-DN4 (2023)"
              >
                <Cpu className="h-2.5 w-2.5 text-blue-600 dark:text-blue-400" />
                CRADIS-DN4 (2023)
              </Badge>
              {journey.score !== undefined && journey.score > 0 && (
                <span className="text-[10px] text-muted-foreground font-semibold">
                  Độ tối ưu: {(journey.score * 10).toFixed(1)}/10
                </span>
              )}
            </div>

            <RouteInfo route={journey.route} />
            <div className="rounded-lg bg-background/60 p-2.5 space-y-2 border border-border/40 text-[11px]">
              {/* Điểm xuất phát của người dùng */}
              {origin?.name && origin.name !== "Origin" && origin.name !== "Location" && (
                <div className="flex items-start gap-1.5 text-emerald-600 dark:text-emerald-400 font-medium">
                  <span className="flex h-4 w-4 shrink-0 items-center justify-center rounded-full bg-emerald-500/15 text-[9px] font-bold mt-0.5">
                    A
                  </span>
                  <div>
                    <span className="font-semibold text-foreground">Điểm xuất phát: </span>
                    <strong className="text-emerald-700 dark:text-emerald-300">{origin.name}</strong>
                    {journey.accessDistanceMeters > 0 ? (
                      <span className="text-muted-foreground text-[10px] block">
                        → Đi bộ ~{journey.accessDistanceMeters}m đến trạm đón <strong>{journey.boardingStop.name}</strong>
                      </span>
                    ) : (
                      <span className="text-muted-foreground text-[10px] block">
                        → Đón xe ngay tại trạm <strong>{journey.boardingStop.name}</strong>
                      </span>
                    )}
                  </div>
                </div>
              )}

              {/* Lên trạm & Xuống trạm */}
              <div className="flex items-center gap-1.5 flex-wrap pt-0.5">
                <span className="font-bold text-foreground">Lên xe:</span>
                <span className="text-blue-600 dark:text-blue-400 font-semibold">{journey.boardingStop.name}</span>
                <span className="text-muted-foreground">→</span>
                <span className="font-bold text-foreground">Xuống xe:</span>
                <span className="text-amber-600 dark:text-amber-400 font-semibold">{journey.alightingStop.name}</span>
              </div>
              <p className="text-muted-foreground text-[10px]">
                {journey.stopCount} chặng giữa trạm · Tuyến chạy thẳng, không đổi tuyến
              </p>

              {/* Điểm đến của người dùng */}
              {destination?.name && destination.name !== "Destination" && destination.name !== "Origin" && destination.name !== "Location" && (
                <div className="flex items-start gap-1.5 text-amber-600 dark:text-amber-400 font-medium pt-1 border-t border-border/30">
                  <span className="flex h-4 w-4 shrink-0 items-center justify-center rounded-full bg-amber-500/15 text-[9px] font-bold mt-0.5">
                    B
                  </span>
                  <div>
                    <span className="font-semibold text-foreground">Điểm đến: </span>
                    <strong className="text-amber-700 dark:text-amber-300">{destination.name}</strong>
                    {journey.egressDistanceMeters > 0 && (
                      <span className="text-muted-foreground text-[10px] block">
                        → Đi bộ ~{journey.egressDistanceMeters}m từ trạm xuống đến điểm đích
                      </span>
                    )}
                  </div>
                </div>
              )}
            </div>

            {/* Nút hành động: Xem đường đi & Đặt điểm hẹn */}
            <div className="pt-2 border-t border-blue-500/20 flex items-center gap-1.5">
              {onSelectJourney && (
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => onSelectJourney(journey, origin, destination)}
                  className="flex-1 h-7 text-[11px] font-medium gap-1 text-blue-600 hover:text-blue-700 border-blue-200 bg-blue-50/60 hover:bg-blue-100/70 dark:text-blue-400 dark:border-blue-800 dark:bg-blue-950/40 cursor-pointer justify-center rounded-lg shadow-2xs"
                >
                  <Compass className="h-3.5 w-3.5 text-primary" />
                  Xem đường đi
                </Button>
              )}

              {onScheduleMeeting && (
                <Button
                  size="sm"
                  onClick={() => {
                    const destName =
                      destination?.name &&
                      destination.name !== "Destination" &&
                      destination.name !== "Origin" &&
                      destination.name !== "Location"
                        ? destination.name
                        : journey.alightingStop.name;
                    onScheduleMeeting({
                      id: String(journey.alightingStop.id),
                      name: destName,
                      address: journey.alightingStop.address,
                      latitude: destination?.latitude ?? journey.alightingStop.latitude,
                      longitude: destination?.longitude ?? journey.alightingStop.longitude,
                    });
                  }}
                  className="flex-1 h-7 text-[11px] font-medium gap-1 bg-black text-white hover:bg-neutral-800 dark:bg-white dark:text-black dark:hover:bg-neutral-200 cursor-pointer justify-center rounded-lg shadow-2xs"
                >
                  <Calendar className="h-3 w-3" />
                  Đặt điểm hẹn
                </Button>
              )}
            </div>
          </div>
        ))}

      {/* 2. CÁC TRẠM DỪNG ĐỘC LẬP (CHỈ HIỂN THỊ KHI NGƯỜI DÙNG TÌM TRẠM, KHÔNG CÓ HÀNH TRÌNH TÌM ĐƯỜNG) */}
      {!hasJourneys &&
        allStops.map(({ stop }, idx) => (
          <div key={`stop-${stop.id}-${idx}`} className="space-y-1.5 rounded-xl border p-2.5 bg-card/60 shadow-xs">
            <p className="font-semibold text-foreground flex items-center gap-1">
              <span className="h-2 w-2 rounded-full bg-blue-500" />
              {stop.mode === "Metro" ? "Ga" : "Trạm"}: {stop.name}
            </p>
            <p className="text-muted-foreground text-[11px]">{stop.address}</p>
            <p className="text-[11px]">Tuyến phục vụ: {stop.routeNumbers.map(formatRouteNumber).join(", ") || "Chưa có"}</p>
            {stop.distanceMeters != null && (
              <p className="text-muted-foreground text-[10px]">
                Cách vị trí khoảng {Math.round(stop.distanceMeters)} m đường thẳng
              </p>
            )}

            <div className="pt-2 border-t border-border/40 flex items-center gap-1.5">
              {onSelectStop && (
                <Button
                  size="sm"
                  variant="outline"
                  onClick={() => onSelectStop(stop)}
                  className="flex-1 h-7 text-[11px] font-medium gap-1 text-blue-600 hover:text-blue-700 border-blue-200 bg-blue-50/60 hover:bg-blue-100/70 dark:text-blue-400 dark:border-blue-800 dark:bg-blue-950/40 cursor-pointer justify-center rounded-lg shadow-2xs"
                >
                  <Compass className="h-3.5 w-3.5 text-primary" />
                  Xem đường đi
                </Button>
              )}

              {onScheduleMeeting && (
                <Button
                  size="sm"
                  onClick={() => {
                    onScheduleMeeting({
                      id: String(stop.id),
                      name: stop.name,
                      address: stop.address,
                      latitude: stop.latitude,
                      longitude: stop.longitude,
                    });
                  }}
                  className="flex-1 h-7 text-[11px] font-medium gap-1 bg-black text-white hover:bg-neutral-800 dark:bg-white dark:text-black dark:hover:bg-neutral-200 cursor-pointer justify-center rounded-lg shadow-2xs"
                >
                  <Calendar className="h-3 w-3" />
                  Đặt điểm hẹn
                </Button>
              )}
            </div>
          </div>
        ))}

      {/* 3. THÔNG TIN TUYẾN XE (CHỈ HIỂN THỊ KHI TRA CỨU TUYẾN XE VÀ KHÔNG CÓ HÀNH TRÌNH TÌM ĐƯỜNG) */}
      {!hasJourneys &&
        allRoutes.map((route, idx) => (
          <div key={`route-${route.patternId}-${idx}`} className="space-y-2 rounded-xl border p-2.5 bg-card/60 shadow-xs">
            <RouteInfo route={route} />
            {route.hasExcludedStops && (
              <p className="text-muted-foreground text-[11px]">
                Chỉ hiển thị trạm hợp lệ trong khu vực hỗ trợ; danh sách này không phải toàn bộ tuyến.
              </p>
            )}
            <details>
              <summary className="cursor-pointer font-medium text-primary text-[11px] py-1">
                Các trạm theo chiều chạy ({route.stops.length})
              </summary>
              <ol className="mt-2 space-y-1.5 pl-1">
                {route.stops.map((stop, i) => (
                  <li key={`route-stop-${stop.id}-${i}`} className="flex items-center justify-between gap-2 p-1.5 rounded-lg border border-border/30 bg-muted/20 text-[11px]">
                    <span className="truncate">
                      {i + 1}. {stop.name}
                    </span>
                    {onSelectStop && (
                      <Button
                        size="sm"
                        variant="outline"
                        onClick={() => onSelectStop(stop)}
                        className="h-6 text-[10px] px-2 gap-1 text-blue-600 hover:text-blue-700 border-blue-200 bg-blue-50/50 hover:bg-blue-100/60 dark:text-blue-400 dark:border-blue-800 dark:bg-blue-950/40 cursor-pointer shrink-0"
                      >
                        <Compass className="h-3 w-3" /> Xem trạm
                      </Button>
                    )}
                  </li>
                ))}
              </ol>
            </details>
          </div>
        ))}

      {statuses.includes("no_network") && <p>Chưa có dữ liệu đã áp dụng cho loại phương tiện này.</p>}
      {statuses.includes("no_results") && <p>Không tìm thấy trạm hoặc tuyến phù hợp trong dữ liệu hiện có.</p>}
      {statuses.includes("no_direct_journey") && (
        <p className="text-muted-foreground">Chưa tìm thấy tuyến đi thẳng phù hợp. Hiện chưa hỗ trợ đổi tuyến.</p>
      )}
      {(statuses.includes("outside_coverage") || statuses.includes("location_required")) && (
        <p className="text-muted-foreground">
          Vui lòng cung cấp vị trí cụ thể trong khu vực hỗ trợ:{" "}
          {supportedAreas.join(", ") || "chưa có khu vực đang hoạt động"}.
        </p>
      )}
    </div>
  );
}
