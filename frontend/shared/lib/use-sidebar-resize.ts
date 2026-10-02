"use client";

import { useCallback, useEffect, useRef, useState } from "react";

const STORAGE_KEY = "losm.sidebarWidth";

export interface UseSidebarResizeOptions {
  readonly defaultWidth?: number;
  readonly minWidth?: number;
  readonly maxWidth?: number;
}

export interface UseSidebarResizeResult {
  readonly width: number;
  readonly startResize: (e: React.MouseEvent) => void;
}

export function useSidebarResize(
  options: UseSidebarResizeOptions = {},
): UseSidebarResizeResult {
  const {
    defaultWidth = 256,
    minWidth = 200,
    maxWidth = 360,
  } = options;
  const [width, setWidth] = useState(defaultWidth);
  const startRef = useRef<{ x: number; w: number } | null>(null);
  const draggingRef = useRef(false);

  useEffect(() => {
    if (typeof window === "undefined") return;
    const stored = window.localStorage.getItem(STORAGE_KEY);
    if (!stored) return;
    const parsed = Number(stored);
    if (!Number.isFinite(parsed)) return;
    setWidth(Math.max(minWidth, Math.min(maxWidth, parsed)));
  }, [minWidth, maxWidth]);

  useEffect(() => {
    if (typeof window === "undefined") return;
    window.localStorage.setItem(STORAGE_KEY, String(width));
  }, [width]);

  const onMove = useCallback(
    (e: MouseEvent) => {
      if (!draggingRef.current || !startRef.current) return;
      const delta = e.clientX - startRef.current.x;
      const next = Math.max(
        minWidth,
        Math.min(maxWidth, startRef.current.w + delta),
      );
      setWidth(next);
    },
    [minWidth, maxWidth],
  );

  const onUp = useCallback(() => {
    if (!draggingRef.current) return;
    draggingRef.current = false;
    startRef.current = null;
    document.body.style.cursor = "";
    document.body.style.userSelect = "";
  }, []);

  useEffect(() => {
    window.addEventListener("mousemove", onMove);
    window.addEventListener("mouseup", onUp);
    return () => {
      window.removeEventListener("mousemove", onMove);
      window.removeEventListener("mouseup", onUp);
    };
  }, [onMove, onUp]);

  const startResize = useCallback(
    (e: React.MouseEvent) => {
      draggingRef.current = true;
      startRef.current = { x: e.clientX, w: width };
      document.body.style.cursor = "col-resize";
      document.body.style.userSelect = "none";
      e.preventDefault();
    },
    [width],
  );

  return { width, startResize };
}
