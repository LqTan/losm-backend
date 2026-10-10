import type { Place } from "@/types/place";

type Props = {
    places: Place[];
    onSelectPlace: (place: Place) => void;
};

export default function SearchResults({
    places,
    onSelectPlace,
}: Props) {
    if (places.length === 0) {
        return (
            <div className="px-1 py-4">
                <p className="text-sm text-gray-500">
                    Không tìm thấy địa điểm phù hợp.
                </p>
            </div>
        );
    }

    return (
        <div>
            {places.map((place) => (
                <button
                    key={place.id}
                    type="button"
                    onClick={() => onSelectPlace(place)}
                    className="
                        flex w-full cursor-pointer gap-4
                        border-b border-gray-100 p-4
                        text-left transition
                        hover:bg-gray-50
                        last:border-b-0
                    "
                >
                    {/* Nội dung */}
                    <div className="min-w-0 flex-1">
                        <h3 className="truncate text-lg font-semibold text-gray-900">
                            {place.name}
                        </h3>

                        {/* Rating */}
                        <div className="mt-1 flex items-center gap-1 text-sm">
                            <span className="font-medium text-gray-800">
                                {place.rating}
                            </span>

                            <span className="text-amber-500">
                                ★
                            </span>

                            <span className="text-gray-500">
                                ({place.reviewCount})
                            </span>
                        </div>

                        {/* Category + distance */}
                        <p className="mt-1 text-sm text-gray-600">
                            {place.category}
                            <span className="mx-1">·</span>
                            {place.distanceKm} km
                        </p>

                        {/* Address */}
                        <p className="mt-1 line-clamp-1 text-sm text-gray-500">
                            {place.address}
                        </p>

                        {/* Opening status */}
                        <div className="mt-2 flex items-center gap-1 text-sm">
                            <span
                                className={
                                    place.isOpen
                                        ? "font-medium text-green-600"
                                        : "font-medium text-red-500"
                                }
                            >
                                {place.isOpen ? "Đang mở" : "Đã đóng"}
                            </span>

                            <span className="text-gray-400">
                                ·
                            </span>

                            <span className="text-gray-500">
                                {place.openingHours}
                            </span>
                        </div>

                        {/* Điểm riêng của app */}
                        <div className="mt-3 flex items-center gap-2">
                            <span className="rounded-full bg-blue-50 px-2.5 py-1 text-xs font-semibold text-blue-700">
                                Phù hợp {Math.round(place.finalScore * 100)}%
                            </span>

                            <span className="rounded-full bg-gray-100 px-2.5 py-1 text-xs text-gray-600">
                                {place.distanceKm} km
                            </span>
                        </div>

                        {/* Lý do đề xuất */}
                        <p className="mt-2 line-clamp-2 text-xs leading-5 text-gray-500">
                            {place.reason}
                        </p>
                    </div>

                    {/* Ảnh */}
                    <img
                        src={place.imageUrl}
                        alt={place.name}
                        className="h-28 w-28 shrink-0 rounded-xl object-cover"
                    />
                </button>
            ))}
        </div>
    );
}
