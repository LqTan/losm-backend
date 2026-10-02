import type {
  MeetingSearchParams,
  Place,
  PlaceSearchParams,
} from "./model";

async function getJson<T>(url: string, init?: RequestInit): Promise<T> {
  const res = await fetch(url, { cache: "no-store", ...init });
  if (!res.ok) {
    throw new Error(`API ${res.status}`);
  }
  return (await res.json()) as T;
}

export async function searchPlaces(
  params: PlaceSearchParams,
): Promise<readonly Place[]> {
  const q = new URLSearchParams();
  q.set("query", params.query);
  if (params.lat !== undefined) q.set("latitude", String(params.lat));
  if (params.lon !== undefined) q.set("longitude", String(params.lon));
  if (params.radiusKm !== undefined) q.set("radiusKm", String(params.radiusKm));
  const data = await getJson<{ items?: Place[] } | Place[]>(
    `/api/proxy/search?${q.toString()}`,
  );
  return Array.isArray(data) ? data : (data.items ?? []);
}

export async function searchMeetingPlaces(
  params: MeetingSearchParams,
): Promise<readonly Place[]> {
  const data = await getJson<{ items?: Place[] } | Place[]>(
    `/api/proxy/search/meeting-places`,
    {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({
        query: params.query,
        attendees: params.attendees,
        radiusKm: params.radiusKm,
      }),
    },
  );
  return Array.isArray(data) ? data : (data.items ?? []);
}

export async function fetchPlace(id: string): Promise<Place | null> {
  try {
    return await getJson<Place>(`/api/proxy/places/${id}`);
  } catch {
    return null;
  }
}
