"use client";

import dynamic from "next/dynamic";

export const MapPanelClient = dynamic(
  () => import("./map-panel").then((m) => m.MapPanel),
  {
    ssr: false,
    loading: () => (
      <div className="h-full w-full animate-pulse bg-muted" aria-hidden />
    ),
  },
);
