import type { Place } from "@/types/place";
import { Clock, Coffee, MapPin, Navigation2, Store, Utensils } from "lucide-react";

type Props = {
  places: Place[];
  onSelectPlace: (place: Place) => void;
};

function getCategoryIcon(category?: string | null) {
  const cat = (category ?? "").toLowerCase();
  if (cat.includes("cafe") || cat.includes("coffee") || cat.includes("cà phê")) {
    return <Coffee className="h-4 w-4 text-muted-foreground" />;
  }
  if (cat.includes("restaurant") || cat.includes("quán ăn") || cat.includes("food") || cat.includes("nhà hàng")) {
    return <Utensils className="h-4 w-4 text-muted-foreground" />;
  }
  return <Store className="h-4 w-4 text-muted-foreground" />;
}

export default function SearchResults({ places, onSelectPlace }: Props) {
  if (places.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center p-8 text-center text-muted-foreground">
        <MapPin className="h-6 w-6 text-muted-foreground mb-2" />
        <p className="text-sm font-medium">Không tìm thấy địa điểm phù hợp</p>
        <p className="text-xs text-muted-foreground mt-0.5">
          Hãy thử đổi từ khóa tìm kiếm
        </p>
      </div>
    );
  }

  return (
    <div className="divide-y divide-border/60">
      {places.map((place) => (
        <button
          key={place.id}
          type="button"
          onClick={() => onSelectPlace(place)}
          className="flex w-full cursor-pointer gap-3 p-3.5 text-left transition hover:bg-muted/40 active:bg-muted/60"
        >
          {/* Category Icon or Image */}
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-muted/50 border border-border">
            {place.imageUrl ? (
              <img
                src={place.imageUrl}
                alt={place.name}
                className="h-full w-full rounded-lg object-cover"
              />
            ) : (
              getCategoryIcon(place.category)
            )}
          </div>

          {/* Details */}
          <div className="min-w-0 flex-1">
            <h3 className="truncate text-sm font-semibold text-foreground">
              {place.name}
            </h3>

            {/* Category & Distance */}
            <div className="mt-0.5 flex items-center gap-1.5 text-xs text-muted-foreground">
              {place.category && (
                <span className="truncate max-w-[140px] font-medium text-foreground/80">
                  {place.category}
                </span>
              )}
              {place.category && place.distanceKm !== undefined && <span>·</span>}
              {place.distanceKm !== undefined && (
                <span className="flex items-center gap-0.5 font-medium text-foreground">
                  <Navigation2 className="h-3 w-3 text-muted-foreground" />
                  {place.distanceKm.toFixed(1)} km
                </span>
              )}
            </div>

            {/* Address */}
            {place.address && (
              <p className="mt-0.5 line-clamp-1 text-xs text-muted-foreground">
                {place.address}
              </p>
            )}

            {/* Hours */}
            {place.openingHours && (
              <p className="mt-0.5 text-[11px] text-muted-foreground flex items-center gap-1 truncate">
                <Clock className="h-3 w-3 shrink-0 text-muted-foreground" />
                <span className="truncate">{place.openingHours}</span>
              </p>
            )}
          </div>
        </button>
      ))}
    </div>
  );
}
