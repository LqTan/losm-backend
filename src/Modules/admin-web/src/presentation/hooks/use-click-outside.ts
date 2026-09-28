"use client";

import { useEffect, useRef, type RefObject } from "react";

/**
 * Invokes the handler on clicks outside the ref. Used by dropdowns and modals.
 * No-op when `enabled` is false so no listener is attached needlessly.
 */
export function useClickOutside<T extends HTMLElement>(
  enabled: boolean,
  handler: () => void,
): RefObject<T | null> {
  const ref = useRef<T | null>(null);
  const handlerRef = useRef(handler);

  useEffect(() => {
    handlerRef.current = handler;
  }, [handler]);

  useEffect(() => {
    if (!enabled) return;

    function onPointerDown(event: MouseEvent | TouchEvent) {
      const element = ref.current;
      if (!element) return;
      if (element.contains(event.target as Node)) return;
      handlerRef.current();
    }

    document.addEventListener("mousedown", onPointerDown);
    document.addEventListener("touchstart", onPointerDown);

    return () => {
      document.removeEventListener("mousedown", onPointerDown);
      document.removeEventListener("touchstart", onPointerDown);
    };
  }, [enabled]);

  return ref;
}
