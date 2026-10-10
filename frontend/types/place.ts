export type Place = {
    id: string | number;
    placeId?: string;
    name: string;
    category?: string | null;
    address?: string | null;
    latitude: number;
    longitude: number;
    distanceKm?: number;
    rating?: number;
    reviewCount?: number;
    isOpen?: boolean;
    openingHours?: string | null;
    relevanceScore?: number;
    distanceScore?: number;
    fairnessScore?: number;
    finalScore?: number;
    imageUrl?: string;
    reason?: string;
};

export interface FriendUser {
    id: string;
    username: string;
    email: string;
    currentLat?: number;
    currentLng?: number;
    addressName?: string;
}

export interface MeetingPlace {
    placeId: string;
    name: string;
    address: string | null;
    latitude: number;
    longitude: number;
    category: string | null;
    openingHours: string | null;
    distanceKmByOrigin: number[];
    averageDistanceKm: number;
    maxDistanceKm: number;
    spreadKm: number;
    relevanceScore: number;
    distanceScore: number;
    fairnessScore: number;
    finalScore: number;
}

export interface SearchMeetingPlacesResponse {
    results: MeetingPlace[];
    centroidLatitude: number;
    centroidLongitude: number;
    searchRadiusKm: number;
    profile: string;
}

export interface ScheduleMeetingInput {
    title: string;
    placeName: string;
    address?: string | null;
    latitude?: number | null;
    longitude?: number | null;
    startAt: string;
    durationMinutes: number;
    attendeeEmails: string[];
    note?: string | null;
    hostName?: string | null;
}
