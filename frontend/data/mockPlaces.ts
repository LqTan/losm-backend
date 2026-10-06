import { Place } from "@/types/place";

export const mockPlaces: Place[] = [
    {
        id: 1,
        name: "The Coffee House",
        category: "Quán cà phê",
        address: "86-88 Cao Thắng, Quận 3, TP.HCM",

        latitude: 10.7765,
        longitude: 106.7009,

        distanceKm: 0.8,

        rating: 4.5,
        reviewCount: 428,

        isOpen: true,
        openingHours: "07:00 - 22:00",

        relevanceScore: 0.94,
        distanceScore: 0.88,
        finalScore: 0.92,

        imageUrl:
            "https://images.unsplash.com/photo-1501339847302-ac426a4a7cbb?w=400",

        reason: "Phù hợp với tìm kiếm quán cà phê gần vị trí hiện tại",
    },

    {
        id: 2,
        name: "Highlands Coffee",
        category: "Quán cà phê",
        address: "135 Nguyễn Huệ, Quận 1, TP.HCM",

        latitude: 10.779,
        longitude: 106.699,

        distanceKm: 1.2,

        rating: 4.3,
        reviewCount: 315,

        isOpen: true,
        openingHours: "07:00 - 23:00",

        relevanceScore: 0.9,
        distanceScore: 0.82,
        finalScore: 0.87,

        imageUrl:
            "https://images.unsplash.com/photo-1495474472287-4d71bcdd2085?w=400",

        reason: "Gần vị trí hiện tại và phù hợp với truy vấn",
    },
];
