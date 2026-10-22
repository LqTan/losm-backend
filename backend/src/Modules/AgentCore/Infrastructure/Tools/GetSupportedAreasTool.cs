using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Tools;
using AgentCore.Application.Tools.GetSupportedAreas;
using Areas.Application.Abstractions;
using Configuration.Application.Abstractions;
using Microsoft.Extensions.Logging;

namespace AgentCore.Infrastructure.Tools;

public sealed class GetSupportedAreasTool
    : AgentTool<GetSupportedAreasToolArguments>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IAreaRepository _areaRepository;
    private readonly ILogger<GetSupportedAreasTool> _logger;

    public GetSupportedAreasTool(
        IAreaRepository areaRepository,
        ITuningProvider tuning,
        ILogger<GetSupportedAreasTool> logger)
        : base(tuning, "get_supported_areas")
    {
        _areaRepository = areaRepository;
        _logger = logger;
    }

    public override string Name => "get_supported_areas";

    public override string Description =>
        "Returns the list of geographic areas (districts, regions) currently supported and active in the system's database. " +
        "Use this tool to check which areas currently have place data coverage, so you can inform or confirm with the user " +
        "when they search for places or meetings.";

    public override ToolDisplay Display => new(
        DisplayName: "Check supported areas",
        CallingMessage: "Checking supported areas...",
        SuccessMessage: "Supported areas found",
        FailedMessage: "Failed to load supported areas",
        PendingConfirmationMessage: "Awaiting confirmation"
    );

    protected override async Task<string> ExecuteAsync(
        GetSupportedAreasToolArguments arguments,
        CancellationToken cancellationToken)
    {
        var allAreas = await _areaRepository.GetAllAsync(cancellationToken);
        var activeOnly = arguments.ActiveOnly ?? true;

        var filtered = activeOnly
            ? allAreas.Where(a => a.IsActive).ToList()
            : allAreas.ToList();

        var result = new
        {
            supportedAreas = filtered.Select(a => new
            {
                id = a.Id,
                name = a.Name,
                level = a.Level.ToString(),
                isActive = a.IsActive,
                boundingBox = new
                {
                    minLat = a.BboxMinLat,
                    minLng = a.BboxMinLng,
                    maxLat = a.BboxMaxLat,
                    maxLng = a.BboxMaxLng
                }
            }).ToList(),
            summary = $"Data is currently available in: {string.Join(", ", filtered.Select(a => a.Name))}."
        };

        return JsonSerializer.Serialize(result, JsonOptions);
    }
}
