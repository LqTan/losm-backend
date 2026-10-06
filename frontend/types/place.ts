export type Place = {
    id: number;
    name: string;
    category: string;
    address: string;

    latitude: number;
    longitude: number;

    distanceKm: number;

    rating: number;
    reviewCount: number;

    isOpen: boolean;
    openingHours: string;

    relevanceScore: number;
    distanceScore: number;
    finalScore: number;

    imageUrl: string;
    reason: string;
};
