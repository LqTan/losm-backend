using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Tools.Transit;
using AgentCore.Infrastructure.AgentRuntime;
using AgentCore.Infrastructure.Tools;
using Areas.Application.Abstractions;
using Areas.Domain.Entities;
using Moq;
using Places.Application.Abstractions;
using Transit.Application.Abstractions;
using Transit.Application.Network.Queries;
using Transit.Domain.Models;

namespace AgentCore.Tests.Tools;

public sealed class TransitToolsTests
{
    private static TransitNetwork Network(bool invalidMiddle = false, bool sequenceGap = false)
    {
        var a = new NetworkStop(Guid.NewGuid(), "A", "Alpha", "First street", 10, 106, "Bus", false);
        var b = new NetworkStop(Guid.NewGuid(), "B", "Beta", "Second street", 10.001, 106, "Bus", invalidMiddle);
        var c = new NetworkStop(Guid.NewGuid(), "C", "Gamma", "Third street", 10.002, 106, "Bus", false);
        var route = new NetworkRoute(Guid.NewGuid(), 1, "Alpha - Gamma", "Bus", 5000, "Reference fare");
        var pattern = new NetworkPattern(Guid.NewGuid(), route.Id, 0, "To Gamma", 300, 1320, 8, 20, "Unknown",
            [new(a.Id, 1), new(b.Id, sequenceGap ? 3 : 2), new(c.Id, sequenceGap ? 4 : 3)]);
        return new([a, b, c], [route], [pattern]);
    }
    private static TransitQueryHandler Handler(TransitNetwork network, bool activeArea = true)
    {
        var repo = new Mock<ITransitNetworkRepository>();
        repo.Setup(x => x.ReadActiveAsync(It.IsAny<string?>(), It.IsAny<CancellationToken>())).ReturnsAsync(network);
        var areas = new Mock<IAreaRepository>();
        areas.Setup(x => x.GetAllAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new[] { new Area("District A", AreaLevel.District, 9.9, 105.9, 10.1, 106.1, isActive: activeArea) });
        var geocoder = new Mock<IGeocodingService>();
        geocoder.Setup(x => x.GeocodeAsync(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync((GeocodeResult?)null);
        return new(repo.Object, areas.Object, geocoder.Object);
    }
    private static TransitQuery Journey(bool reverse = false) => new("journeys", Origin: new(Latitude: reverse ? 10.002 : 10, Longitude: 106),
        Destination: new(Latitude: reverse ? 10 : 10.002, Longitude: 106), RadiusMeters: 50);

    [Fact]
    public async Task StopsExcludeUnsafeDataAndExposeProximityAsStraightLine()
    {
        var result = await Handler(Network(true)).HandleAsync(new("stops", Query: "street"));
        Assert.Equal(2, result.Stops.Count);
        Assert.DoesNotContain(result.Stops, s => s.Name == "Beta");
        Assert.Equal("straight_line", result.DistanceKind);
        Assert.False(result.IsRealtime);
    }
    [Fact]
    public async Task DirectJourneyRespectsOrderAndReportsBoardingAndAlighting()
    {
        var result = await Handler(Network()).HandleAsync(Journey());
        var journey = Assert.Single(result.Journeys);
        Assert.Equal("Alpha", journey.BoardingStop.Name);
        Assert.Equal("Gamma", journey.AlightingStop.Name);
        Assert.Equal(2, journey.StopCount);
        Assert.Equal(3, journey.Route.Stops.Count);
        Assert.Equal(1, journey.Route.Number);
        Assert.Equal(0, journey.AccessDistanceMeters);
        Assert.Equal("no_direct_journey", (await Handler(Network()).HandleAsync(Journey(true))).Status);
    }
    [Fact]
    public async Task UnsafeIntermediateStopCannotBeSkippedToInventAConnection()
    {
        var result = await Handler(Network(true)).HandleAsync(Journey());
        Assert.Empty(result.Journeys);
        Assert.Equal("no_direct_journey", result.Status);
    }
    [Fact]
    public async Task MissingSequenceCannotBeBridged()
    {
        Assert.Empty((await Handler(Network(sequenceGap: true)).HandleAsync(Journey())).Journeys);
    }
    [Fact]
    public async Task OutOfAreaIntermediateStopCannotBeTraversed()
    {
        var network = Network();
        network = network with { Stops = network.Stops.Select(s => s.Name == "Beta" ? s with { Latitude = 11 } : s).ToArray() };
        Assert.Empty((await Handler(network).HandleAsync(Journey())).Journeys);
    }
    [Fact]
    public async Task UnsupportedLocationRequestsClarificationWithCoverage()
    {
        var result = await Handler(Network()).HandleAsync(new("stops", Origin: new(Latitude: 20, Longitude: 106)));
        Assert.Equal("outside_coverage", result.Status);
        Assert.True(result.RequiresLocationConfirmation);
        Assert.Contains("District A", result.SupportedAreas);
    }
    [Fact]
    public async Task InactiveDistrictIsNotAvailableForSearch()
    {
        var result = await Handler(Network(), false).HandleAsync(new("stops", Query: "Alpha"));
        Assert.Equal("outside_coverage", result.Status);
        Assert.Empty(result.Stops);
    }
    [Fact]
    public async Task StationNamesResolveWithoutExternalGeocoding()
    {
        var result = await Handler(Network()).HandleAsync(new("journeys", Origin: new("Alpha"), Destination: new("Gamma"), RadiusMeters: 50));
        Assert.Single(result.Journeys);
    }
    [Fact]
    public async Task UnknownPlaceDoesNotReceiveInventedCoordinates()
    {
        var result = await Handler(Network()).HandleAsync(new("journeys", Origin: new("Missing place"), Destination: new("Gamma")));
        Assert.Equal("location_required", result.Status);
        Assert.True(result.RequiresLocationConfirmation);
        Assert.Empty(result.Journeys);
    }
    [Fact]
    public async Task RouteLookupIndicatesPartialStopList()
    {
        var result = await Handler(Network(true)).HandleAsync(new("routes", RouteNumber: 1));
        var route = Assert.Single(result.Routes);
        Assert.True(route.HasExcludedStops);
        Assert.Equal(2, route.Stops.Count);
        Assert.Equal(5000, route.ReferenceFare);
    }
    [Fact]
    public async Task EmptyPublishedNetworkReturnsNoNetwork()
    {
        var result = await Handler(new([], [], [])).HandleAsync(new("stops", Query: "Alpha"));
        Assert.Equal("no_network", result.Status);
    }
    [Fact]
    public async Task ToolsAreRegisteredReadOnlyAndMissingGpsAsksForLocation()
    {
        var handler = Handler(Network());
        var context = new AgentExecutionContext();
        var tools = new IAgentTool[] { new SearchTransitStopsTool(handler, context), new GetTransitRoutesTool(handler), new FindTransitJourneyTool(handler, context) };
        var registry = new AgentToolRegistry(tools);
        Assert.Equal(3, registry.GetAll().Count);
        Assert.All(tools, t => { Assert.Equal(ToolKind.ReadOnly, t.Kind); Assert.False(t.SuppliesPlaces); Assert.NotNull(t.ParametersSchema); });
        using var args = JsonDocument.Parse("{}");
        using var result = JsonDocument.Parse(await registry.GetRequired("search_transit_stops").ExecuteAsync(args.RootElement));
        Assert.Equal("location_required", result.RootElement.GetProperty("status").GetString());
    }
}
