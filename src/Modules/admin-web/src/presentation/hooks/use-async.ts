"use client";

import { useCallback, useEffect, useState } from "react";
import { toDisplayMessage } from "@/domain/errors/app-error";

export interface AsyncState<T> {
  readonly data: T | null;
  readonly error: string | null;
  readonly isLoading: boolean;
}

export interface UseAsyncResult<T> extends AsyncState<T> {
  reload: () => void;
  setData: (data: T) => void;
}

/**
 * Runs an async task for the lifetime of a component.
 *
 * Rule: the effect only invokes the task and never calls setState
 * synchronously — state updates happen after the first await. This avoids
 * cascading renders and keeps react-hooks/set-state-in-effect quiet.
 *
 * `deps` is the source of truth for when to re-run; `task` is only read
 * inside the effect so it does not need to be listed in deps.
 */
export function useAsync<T>(
  task: () => Promise<T>,
  deps: readonly unknown[],
): UseAsyncResult<T> {
  const [state, setState] = useState<AsyncState<T>>({
    data: null,
    error: null,
    isLoading: true,
  });
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let cancelled = false;

    void (async () => {
      try {
        const result = await task();
        if (!cancelled) {
          setState({ data: result, error: null, isLoading: false });
        }
      } catch (error) {
        if (!cancelled) {
          setState({
            data: null,
            error: toDisplayMessage(error),
            isLoading: false,
          });
        }
      }
    })();

    return () => {
      cancelled = true;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [...deps, reloadKey]);

  const reload = useCallback(() => {
    setState((current) => ({ ...current, isLoading: true, error: null }));
    setReloadKey((key) => key + 1);
  }, []);

  const setData = useCallback((data: T) => {
    setState((current) => ({ ...current, data, isLoading: false }));
  }, []);

  return { ...state, reload, setData };
}
