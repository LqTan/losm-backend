using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Tools.GeocodePlace;
using AgentCore.Infrastructure.Tools;
using AgentCore.Tests.TestSupport;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Places.Application.Abstractions;

namespace AgentCore.Tests.Tools;

public class GeocodePlaceToolTests
{
    [Fact]
    public async Task ExecuteAsync_WhenGeocodeReturnsResult_SerializesCoordinates()
    {
        var geocoder = new Mock<IGeocodingService>();
        geocoder
            .Setup(x => x.GeocodeAsync(
                "Quận 1",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new GeocodedPlace(
                "relation",
                2216341,
                "Quận 1",
                "Quận 1, Hồ Chí Minh, Việt Nam",
                10.7769,
                106.7009,
                "boundary",
                "administrative"));

        var tool = new GeocodePlaceTool(
            geocoder.Object,
            new ConfigurationStub(),
            NullLogger<GeocodePlaceTool>.Instance);

        var argsJson = JsonSerializer.Serialize(new GeocodePlaceToolArguments("Quận 1"));
        using var doc = JsonDocument.Parse(argsJson);

        var result = await tool.ExecuteAsync(doc.RootElement, CancellationToken.None);

        using var resultDoc = JsonDocument.Parse(result);
        var root = resultDoc.RootElement;

        Assert.False(root.GetProperty("notFound").GetBoolean());
        Assert.Equal(10.7769, root.GetProperty("latitude").GetDouble(), 4);
        Assert.Equal(106.7009, root.GetProperty("longitude").GetDouble(), 4);
    }

    [Fact]
    public async Task ExecuteAsync_WhenGeocodeReturnsNull_ReturnsNotFoundPayload()
    {
        var geocoder = new Mock<IGeocodingService>();
        geocoder
            .Setup(x => x.GeocodeAsync(
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync((GeocodedPlace?)null);

        var tool = new GeocodePlaceTool(
            geocoder.Object,
            new ConfigurationStub(),
            NullLogger<GeocodePlaceTool>.Instance);

        var argsJson = JsonSerializer.Serialize(new GeocodePlaceToolArguments("nowhere"));
        using var doc = JsonDocument.Parse(argsJson);

        var result = await tool.ExecuteAsync(doc.RootElement, CancellationToken.None);

        using var resultDoc = JsonDocument.Parse(result);
        var root = resultDoc.RootElement;

        Assert.True(root.GetProperty("notFound").GetBoolean());
    }
}
