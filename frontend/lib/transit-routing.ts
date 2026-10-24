/**
 * Transit routing helper using OSRM to generate realistic road & sidewalk polylines
 * instead of straight diagonal lines cutting across city blocks.
 */

function calculateDistanceMeters(lat1: number, lon1: number, lat2: number, lon2: number): number {
  const R = 6371000;
  const dLat = ((lat2 - lat1) * Math.PI) / 180;
  const dLon = ((lon2 - lon1) * Math.PI) / 180;
  const a =
    Math.sin(dLat / 2) * Math.sin(dLat / 2) +
    Math.cos((lat1 * Math.PI) / 180) *
      Math.cos((lat2 * Math.PI) / 180) *
      Math.sin(dLon / 2) *
      Math.sin(dLon / 2);
  const c = 2 * Math.atan2(Math.sqrt(a), Math.sqrt(1 - a));
  return R * c;
}

/**
 * Fetch pedestrian route along sidewalks and roads between two points using OSRM foot routing.
 */
export async function fetchPedestrianStreetRoute(
  fromLat: number,
  fromLng: number,
  toLat: number,
  toLng: number
): Promise<[number, number][]> {
  const dist = calculateDistanceMeters(fromLat, fromLng, toLat, toLng);
  // If the points are virtually at the same spot (< 30m), no walking line is needed.
  if (dist < 30) {
    return [];
  }

  const straightFallback: [number, number][] = [
    [fromLat, fromLng],
    [toLat, toLng],
  ];

  try {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 3500);

    const url = `https://router.project-osrm.org/route/v1/foot/${fromLng},${fromLat};${toLng},${toLat}?overview=full&geometries=geojson`;
    const res = await fetch(url, { signal: controller.signal });
    clearTimeout(timeout);

    if (!res.ok) return straightFallback;
    const data = await res.json();
    if (data.code === "Ok" && data.routes?.[0]?.geometry?.coordinates) {
      const coords = data.routes[0].geometry.coordinates.map(
        ([lng, lat]: [number, number]) => [lat, lng] as [number, number]
      );
      if (coords.length > 1) {
        return coords;
      }
    }
  } catch (e) {
    console.warn("OSRM foot routing failed, using straight-line fallback:", e);
  }

  return straightFallback;
}

/**
 * Fetch bus driving route along actual streets through the sequence of bus stops using OSRM.
 */
export async function fetchBusStreetRoute(
  stops: [number, number][]
): Promise<[number, number][]> {
  if (stops.length < 2) return stops;

  try {
    const controller = new AbortController();
    const timeout = setTimeout(() => controller.abort(), 4000);

    // Limit waypoints to max 10 to keep OSRM fast and reliable
    const sampledStops =
      stops.length <= 10
        ? stops
        : [
            stops[0],
            ...stops
              .slice(1, -1)
              .filter((_, idx, arr) => idx % Math.ceil(arr.length / 8) === 0),
            stops[stops.length - 1],
          ];

    const coordStr = sampledStops.map(([lat, lng]) => `${lng},${lat}`).join(";");
    const url = `https://router.project-osrm.org/route/v1/driving/${coordStr}?overview=full&geometries=geojson`;

    const res = await fetch(url, { signal: controller.signal });
    clearTimeout(timeout);

    if (!res.ok) return stops;
    const data = await res.json();
    if (data.code === "Ok" && data.routes?.[0]?.geometry?.coordinates) {
      const coords = data.routes[0].geometry.coordinates.map(
        ([lng, lat]: [number, number]) => [lat, lng] as [number, number]
      );
      if (coords.length > 1) {
        return coords;
      }
    }
  } catch (e) {
    console.warn("OSRM driving routing failed, using stop sequence fallback:", e);
  }

  return stops;
}
