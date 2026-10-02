"use client";

import { useEffect, useState } from "react";
import { toast } from "sonner";

export interface CurrentLocationState {
  readonly loading: boolean;
  readonly lat: number | null;
  readonly lon: number | null;
  readonly error: string | null;
}

const FALLBACK: CurrentLocationState = {
  loading: false,
  lat: null,
  lon: null,
  error: null,
};

export function useCurrentLocation(auto = false): CurrentLocationState & {
  request: () => void;
} {
  const [state, setState] = useState<CurrentLocationState>(FALLBACK);

  const request = () => {
    if (typeof navigator === "undefined" || !navigator.geolocation) {
      setState({ loading: false, lat: null, lon: null, error: "Trình duyệt không hỗ trợ định vị." });
      return;
    }
    setState((s) => ({ ...s, loading: true, error: null }));
    navigator.geolocation.getCurrentPosition(
      (pos) =>
        setState({
          loading: false,
          lat: pos.coords.latitude,
          lon: pos.coords.longitude,
          error: null,
        }),
      (err) => {
        setState({
          loading: false,
          lat: null,
          lon: null,
          error: err.message || "Không lấy được vị trí.",
        });
        toast.error("Không lấy được vị trí. Dùng vị trí mặc định.");
      },
      { enableHighAccuracy: false, timeout: 10_000 },
    );
  };

  useEffect(() => {
    if (auto) request();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return { ...state, request };
}
