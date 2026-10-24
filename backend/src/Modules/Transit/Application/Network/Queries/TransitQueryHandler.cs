using System.Globalization;
using System.Text;
using Areas.Application.Abstractions;
using Areas.Domain.Entities;
using Places.Application.Abstractions;
using Transit.Application.Abstractions;
using Transit.Domain.Models;

namespace Transit.Application.Network.Queries;

public sealed class TransitQueryHandler(ITransitNetworkRepository repository, IAreaRepository areas, IGeocodingService geocoder)
{
    public async Task<TransitQueryResult> HandleAsync(TransitQuery query, CancellationToken ct = default)
    {
        if (query.Mode is not (null or "Bus" or "Metro")) throw new ArgumentException("Mode must be Bus or Metro.");
        if (query.Kind is not ("stops" or "routes" or "journeys")) throw new ArgumentException("Unsupported transit query.");
        if (!double.IsFinite(query.RadiusMeters) || query.RadiusMeters is < 50 or > 3000) throw new ArgumentException("Radius must be between 50 and 3000 meters.");
        if (query.RouteNumber is <= 0) throw new ArgumentException("Route number must be positive.");
        var limit = Math.Clamp(query.Limit, 1, 20);
        var supported = (await areas.GetAllAsync(ct)).Where(a => a.IsActive && a.Level == AreaLevel.District).ToArray();
        var names = supported.Select(a => a.Name).ToArray();
        TransitQueryResult Empty(string status, string message, bool confirm = false) => new(query.Kind, status, message, names, [], [], [], confirm);
        if (supported.Length == 0) return Empty("outside_coverage", "No active districts are configured. Ask the user to choose a supported district.", true);
        var network = await repository.ReadActiveAsync(query.Mode, ct);
        if (network.Routes.Count == 0) return Empty("no_network", "No published transit network is available for this mode. Import and apply a dataset first.");
        bool Inside(double lat, double lon) => supported.Any(a => lat >= a.BboxMinLat && lat <= a.BboxMaxLat && lon >= a.BboxMinLng && lon <= a.BboxMaxLng);
        bool Usable(NetworkStop s) => !s.IsNotSafe && Inside(s.Latitude, s.Longitude);
        var stopById = network.Stops.ToDictionary(s => s.Id);
        var routeById = network.Routes.ToDictionary(r => r.Id);
        var safeStops = network.Stops.Where(Usable).ToArray();
        var routesAtStop = network.Patterns.SelectMany(p => p.Calls.Select(c => (c.StopId, Number: routeById[p.RouteId].Number)))
            .ToLookup(c => c.StopId, c => c.Number);
        TransitStopResult Stop(NetworkStop s, double? distance = null) => new(s.Id, s.Name, s.Address, s.Latitude, s.Longitude,
            s.Mode, distance is null ? null : Math.Round(distance.Value), routesAtStop[s.Id].Distinct().Order().ToArray());
        TransitRouteResult Route(NetworkPattern p, IEnumerable<NetworkCall>? segment = null)
        {
            var r = routeById[p.RouteId];
            var calls = (segment ?? p.Calls).ToArray();
            return new(r.Id, r.Number, r.Name, r.Mode, p.Id, p.Direction, p.Name, p.FirstMinute, p.LastMinute,
                p.HeadwayMin, p.HeadwayMax, p.ServiceDays, r.ReferenceFare, r.FareDescription,
                calls.Any(c => !stopById.TryGetValue(c.StopId, out var s) || !Usable(s)),
                calls.Where(c => stopById.TryGetValue(c.StopId, out var s) && Usable(s)).Select(c => Stop(stopById[c.StopId])).ToArray());
        }
        if (query.Kind == "routes")
        {
            var term = Normalize(query.Query ?? "");
            var found = network.Patterns.Where(p => p.Calls.Any(c => stopById.TryGetValue(c.StopId, out var s) && Usable(s)))
                .Where(p => query.RouteNumber is null || routeById[p.RouteId].Number == query.RouteNumber)
                .Where(p => term.Length == 0 || Normalize(routeById[p.RouteId].Name + " " + p.Name).Contains(term))
                .OrderBy(p => routeById[p.RouteId].Number).ThenBy(p => p.Direction).Take(limit).Select(p => Route(p)).ToArray();
            return new(query.Kind, found.Length > 0 ? "ok" : "no_results", "Published route information. Times are terminal service windows, not arrivals. Fares are references, not guaranteed journey prices.", names, [], found, []);
        }
        async Task<(double Lat, double Lon, string Name)?> Resolve(TransitLocation? location, string fallbackName = "Location")
        {
            if (location is null) return null;
            if (location.Latitude.HasValue || location.Longitude.HasValue)
            {
                if (location.Latitude is not { } lat || location.Longitude is not { } lon || !double.IsFinite(lat) || !double.IsFinite(lon) || lat is < -90 or > 90 || lon is < -180 or > 180 || (lat == 0 && lon == 0)) return null;
                return (lat, lon, !string.IsNullOrWhiteSpace(location.Name) ? location.Name : fallbackName);
            }
            if (string.IsNullOrWhiteSpace(location.Name)) return null;
            var place = await geocoder.GeocodeAsync(location.Name, ct);
            if (place is not null) return (place.Latitude, place.Longitude, place.Name ?? location.Name);
            var exact = safeStops.Where(s => Normalize(s.Name) == Normalize(location.Name) || s.ExternalId.Equals(location.Name, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (exact.Length == 1) return (exact[0].Latitude, exact[0].Longitude, exact[0].Name);
            return null;
        }
        var origin = await Resolve(query.Origin, "Origin");
        if (query.Origin is not null && origin is null) return Empty("location_required", "The origin is missing, ambiguous or absent from internal place data. Ask for a specific place or coordinates.", true);
        if (origin is { } orig && !Inside(orig.Lat, orig.Lon)) return Empty("outside_coverage", "The origin is outside active districts. Explain available districts and ask for a supported location.", true);
        if (query.Kind == "stops")
        {
            if (origin is null && string.IsNullOrWhiteSpace(query.Query)) return Empty("location_required", "Ask for the user's location or a stop name; never invent coordinates.", true);
            var term = Normalize(query.Query ?? "");
            var found = safeStops.Where(s => term.Length == 0 || Normalize(s.Name + " " + s.Address + " " + s.ExternalId).Contains(term))
                .Select(s => (Stop: s, Distance: origin is { } point ? (double?)Distance(point.Lat, point.Lon, s.Latitude, s.Longitude) : null))
                .Where(x => x.Distance is null || x.Distance <= query.RadiusMeters)
                .OrderBy(x => x.Distance).ThenBy(x => x.Stop.Name).Take(limit).Select(x => Stop(x.Stop, x.Distance)).ToArray();
            var originLoc = origin is { } p ? new TransitLocation(p.Name, p.Lat, p.Lon) : null;
            return new(query.Kind, found.Length > 0 ? "ok" : "no_results", "Stops from published internal data. Distances are straight-line proximity, not verified walking routes.", names, found, [], [], Origin: originLoc);
        }
        if (origin is null) return Empty("location_required", "Ask for a starting location.", true);
        var destination = await Resolve(query.Destination, "Destination");
        if (destination is null) return Empty("location_required", "Ask for a specific destination available in internal data.", true);
        if (!Inside(destination.Value.Lat, destination.Value.Lon)) return Empty("outside_coverage", "The destination is outside active districts. Ask for a supported location.", true);
        var access = safeStops.Select(s => (s.Id, Distance: Distance(origin.Value.Lat, origin.Value.Lon, s.Latitude, s.Longitude)))
            .Where(x => x.Distance <= query.RadiusMeters).ToDictionary(x => x.Id, x => x.Distance);
        var egress = safeStops.Select(s => (s.Id, Distance: Distance(destination.Value.Lat, destination.Value.Lon, s.Latitude, s.Longitude)))
            .Where(x => x.Distance <= query.RadiusMeters).ToDictionary(x => x.Id, x => x.Distance);
        var candidates = new List<TransitJourneyResult>();
        foreach (var p in network.Patterns)
        {
            ct.ThrowIfCancellationRequested();
            var calls = p.Calls.OrderBy(c => c.Sequence).ToArray();
            var options = new List<TransitJourneyResult>();
            for (var i = 0; i < calls.Length; i++)
            {
                if (!access.TryGetValue(calls[i].StopId, out var startDistance)) continue;
                for (var j = i + 1; j < calls.Length; j++)
                {
                    if (calls[j].Sequence != calls[j - 1].Sequence + 1 || !stopById.TryGetValue(calls[j].StopId, out var next) || !Usable(next)) break;
                    if (calls[i].StopId == calls[j].StopId || !egress.TryGetValue(calls[j].StopId, out var endDistance)) continue;
                    options.Add(new(Route(p, calls[i..(j + 1)]), Stop(stopById[calls[i].StopId], startDistance),
                        Stop(next, endDistance), j - i, Math.Round(startDistance), Math.Round(endDistance)));
                }
            }
            var best = options.OrderBy(x => x.AccessDistanceMeters + x.EgressDistanceMeters).ThenBy(x => x.StopCount).FirstOrDefault();
            if (best is not null) candidates.Add(best);
        }
        var journeys = candidates.OrderBy(x => x.AccessDistanceMeters + x.EgressDistanceMeters).ThenBy(x => x.StopCount).Take(limit).ToArray();
        var originLocation = origin is { } startPoint ? new TransitLocation(startPoint.Name, startPoint.Lat, startPoint.Lon) : null;
        var destinationLocation = destination is { } endPoint ? new TransitLocation(endPoint.Name, endPoint.Lat, endPoint.Lon) : null;

        return new(query.Kind, journeys.Length > 0 ? "ok" : "no_direct_journey",
            "Direct routes only, in the recorded direction and stop order, within active districts. No transfers, live arrivals, journey duration or verified walking route is available. Service windows do not guarantee operation at the requested time.", names, [], [], journeys,
            Origin: originLocation, Destination: destinationLocation);
    }
    private static string Normalize(string input) => string.Concat(input.ToLowerInvariant().Normalize(NormalizationForm.FormD)
        .Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)).Replace('\u0111', 'd').Normalize(NormalizationForm.FormC);
    private static double Distance(double lat1, double lon1, double lat2, double lon2)
    {
        const double radians = Math.PI / 180;
        var a = Math.Pow(Math.Sin((lat2 - lat1) * radians / 2), 2) + Math.Cos(lat1 * radians) * Math.Cos(lat2 * radians) * Math.Pow(Math.Sin((lon2 - lon1) * radians / 2), 2);
        return 6371000 * 2 * Math.Asin(Math.Sqrt(Math.Clamp(a, 0, 1)));
    }
}
