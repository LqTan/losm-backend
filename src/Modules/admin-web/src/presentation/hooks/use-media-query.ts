"use client";

import { useCallback, useSyncExternalStore } from "react";

/**
 * Tracks a media query with useSyncExternalStore — the correct way to read
 * external state (window.matchMedia) without setState inside an effect.
 *
 * getServerSnapshot returns false so the server render always matches
 * hydration. Only use it for things that do not affect the initial markup
 * (e.g. the mobile sidebar); do not use it to pick a render branch.
 */
export function useMediaQuery(query: string): boolean {
  const subscribe = useCallback(
    (onStoreChange: () => void) => {
      const list = window.matchMedia(query);
      list.addEventListener("change", onStoreChange);
      return () => list.removeEventListener("change", onStoreChange);
    },
    [query],
  );

  const getSnapshot = useCallback(
    () => window.matchMedia(query).matches,
    [query],
  );

  const getServerSnapshot = useCallback(() => false, []);

  return useSyncExternalStore(subscribe, getSnapshot, getServerSnapshot);
}

/** Mazer's xl breakpoint, where the sidebar collapses off-canvas. */
export function useIsDesktop(): boolean {
  return useMediaQuery("(min-width: 1200px)");
}
