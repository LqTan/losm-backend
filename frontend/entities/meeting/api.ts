import type {
  CreateReviewInput,
  GoogleCalendarStatus,
  Meeting,
  PendingActionDetail,
  Review,
  SavedPlace,
} from "./model";

async function req<T>(input: RequestInfo, init?: RequestInit): Promise<T> {
  const res = await fetch(input, {
    cache: "no-store",
    ...init,
    headers: {
      "Content-Type": "application/json",
      ...(init?.headers ?? {}),
    },
  });
  if (!res.ok) throw new Error(`API ${res.status}`);
  return (await res.json()) as T;
}

export async function fetchMeetings(scope: "upcoming" | "past"): Promise<readonly Meeting[]> {
  const data = await req<{ items?: Meeting[] } | Meeting[]>(
    `/api/proxy/users/me/meetings?status=${scope}`,
  );
  return Array.isArray(data) ? data : (data.items ?? []);
}

export async function fetchPendingAction(actionId: string): Promise<PendingActionDetail> {
  return req<PendingActionDetail>(
    `/api/proxy/agent/pending-actions/${actionId}`,
  );
}

export async function confirmMeeting(input: {
  actionId: string;
  title: string;
  startAt: string;
  attendeesRaw: string;
  notes: string;
}): Promise<{ actionId: string }> {
  return req<{ actionId: string }>("/api/proxy/agent/confirm", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export async function retryMeetingEmails(actionId: string): Promise<{ actionId: string }> {
  return req<{ actionId: string }>("/api/proxy/agent/retry-emails", {
    method: "POST",
    body: JSON.stringify({ originalActionId: actionId }),
  });
}

export async function fetchReviewsForPlace(
  placeId: string,
): Promise<readonly Review[]> {
  const data = await req<{ items?: Review[] } | Review[]>(
    `/api/proxy/reviews/place/${placeId}`,
  );
  return Array.isArray(data) ? data : (data.items ?? []);
}

export async function fetchAverageRating(placeId: string): Promise<number | null> {
  try {
    const data = await req<{ average: number }>(
      `/api/proxy/reviews/place/${placeId}/average-rating`,
    );
    return data.average;
  } catch {
    return null;
  }
}

export async function createReview(input: CreateReviewInput): Promise<Review> {
  return req<Review>("/api/proxy/reviews", {
    method: "POST",
    body: JSON.stringify(input),
  });
}

export async function fetchMyReviews(): Promise<readonly Review[]> {
  const data = await req<{ items?: Review[] } | Review[]>(
    `/api/proxy/users/me/reviews`,
  );
  return Array.isArray(data) ? data : (data.items ?? []);
}

export async function fetchSavedPlaces(): Promise<readonly SavedPlace[]> {
  const data = await req<{ items?: SavedPlace[] } | SavedPlace[]>(
    `/api/proxy/saved-places`,
  );
  return Array.isArray(data) ? data : (data.items ?? []);
}

export async function savePlace(placeId: string): Promise<void> {
  await req<unknown>("/api/proxy/saved-places", {
    method: "POST",
    body: JSON.stringify({ placeId }),
  });
}

export async function unsavePlace(placeId: string): Promise<void> {
  await req<unknown>(`/api/proxy/saved-places/${placeId}`, {
    method: "DELETE",
  });
}

export async function fetchGoogleCalendarStatus(): Promise<GoogleCalendarStatus> {
  return req<GoogleCalendarStatus>(
    `/api/proxy/users/me/google-calendar/status`,
  );
}
