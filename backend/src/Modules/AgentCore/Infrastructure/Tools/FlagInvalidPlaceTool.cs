using System.Text.Json;
using AgentCore.Application.Abstractions;
using AgentCore.Application.Tools;
using AgentCore.Application.Tools.FlagInvalidPlace;
using Configuration.Application.Abstractions;
using Microsoft.Extensions.Logging;
using Places.Application.Abstractions;

namespace AgentCore.Infrastructure.Tools;

public sealed class FlagInvalidPlaceTool
    : AgentTool<FlagInvalidPlaceToolArguments>
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    private readonly IPlaceRepository _placeRepository;
    private readonly ILogger<FlagInvalidPlaceTool> _logger;

    public FlagInvalidPlaceTool(
        IPlaceRepository placeRepository,
        ITuningProvider tuning,
        ILogger<FlagInvalidPlaceTool> logger)
        : base(tuning, "flag_invalid_place")
    {
        _placeRepository = placeRepository;
        _logger = logger;
    }

    public override string Name => "flag_invalid_place";

    public override string Description =>
        "Đánh dấu một địa điểm là không hợp lệ / sai lệch dữ liệu (IsNotSafe) và loại bỏ vĩnh viễn khỏi các kết quả tìm kiếm của hệ thống. " +
        "CHỈ sử dụng công cụ này khi phát hiện lỗi dữ liệu kỹ thuật nghiêm trọng: " +
        "(1) Toạ độ cắm trong khu vực Quận đang hỗ trợ nhưng Tên/Địa chỉ lại thuộc quận khác (ví dụ: Tân Phú, Bình Tân, Quận 4, Quận 12...). " +
        "(2) Toạ độ của địa danh nổi tiếng bị cắm sai lệch nghiêm trọng sang khu vực khác. " +
        "TUYỆT ĐỐI KHÔNG dùng công cụ này cho địa điểm chỉ đơn giản là không hợp gu người dùng.";

    public override ToolKind Kind => ToolKind.WriteAutoApply;

    public override ToolDisplay Display => new(
        DisplayName: "Flag invalid place",
        CallingMessage: "Flagging invalid place data...",
        SuccessMessage: "Invalid place excluded from search",
        FailedMessage: "Failed to flag place",
        PendingConfirmationMessage: "Awaiting confirmation"
    );

    protected override async Task<string> ExecuteAsync(
        FlagInvalidPlaceToolArguments arguments,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(arguments.Reason))
        {
            throw new ArgumentException("A reason for flagging the place is required.", nameof(arguments.Reason));
        }

        Places.Domain.Entities.Place? place = null;

        if (!string.IsNullOrWhiteSpace(arguments.PlaceId) && Guid.TryParse(arguments.PlaceId, out var id))
        {
            place = await _placeRepository.GetByIdAsync(id, cancellationToken);
        }

        if (place is null && !string.IsNullOrWhiteSpace(arguments.PlaceName))
        {
            var matches = await _placeRepository.SearchByNameAsync(arguments.PlaceName, 1, cancellationToken);
            place = matches.FirstOrDefault();
        }

        if (place is null)
        {
            _logger.LogWarning("FlagInvalidPlaceTool: Place not found {PlaceId} / {PlaceName}", arguments.PlaceId, arguments.PlaceName);
            return JsonSerializer.Serialize(new
            {
                success = false,
                message = "No matching place found to flag."
            }, JsonOptions);
        }

        place.MarkAsNotSafe(arguments.Reason);
        await _placeRepository.UpdateAsync(place, cancellationToken);

        _logger.LogInformation("FlagInvalidPlaceTool: Flagged '{PlaceName}' ({PlaceId}) as unsafe: {Reason}", place.Name, place.Id, arguments.Reason);

        return JsonSerializer.Serialize(new
        {
            success = true,
            placeId = place.Id,
            placeName = place.Name,
            reason = arguments.Reason,
            message = $"Place '{place.Name}' marked invalid and excluded from search."
        }, JsonOptions);
    }
}
