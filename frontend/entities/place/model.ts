import type { PlaceId } from "@/shared/lib/brands";

export interface GeoPoint {
  readonly lat: number;
  readonly lon: number;
}

export interface Place {
  readonly id: PlaceId;
  readonly name: string;
  readonly address: string;
  readonly location: GeoPoint;
  readonly rating: number;
  readonly distanceKm: number;
  readonly categories: readonly string[];
  readonly thumbnailUrl: string | null;
  readonly finalScore?: number;
}

export interface MeetingCandidate {
  readonly attendeeLocations: readonly GeoPoint[];
  readonly place: Place;
  readonly fairScore: number;
}

export interface PlaceSearchParams {
  query: string;
  lat?: number;
  lon?: number;
  radiusKm?: number;
}

export interface MeetingSearchParams {
  query: string;
  attendees: readonly GeoPoint[];
  radiusKm?: number;
}
