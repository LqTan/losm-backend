using Microsoft.AspNetCore.Http;

namespace Search.Application.Abstractions;

public sealed record OnnxModelStatusDto(
    bool HasModel,
    bool IsActive,
    string? ModelName,
    long FileSizeBytes,
    DateTime? UploadedAt,
    string? InputName,
    int? FeatureCount,
    string? StatusMessage
);

public sealed record RankingFeaturesDto(
    float Bm25Norm,
    float CategoryMatch,
    float DurationAvgMin,
    float DurationMaxMin,
    float DurationSpreadMin,
    float DistanceAvgKm,
    float DistanceMaxKm,
    float DurationAvgRankNorm,
    float DurationSpreadRankNorm,
    float ParticipantCount
);

public interface IOnnxRankingModelService
{
    OnnxModelStatusDto GetStatus();
    Task<OnnxModelStatusDto> UploadModelAsync(IFormFile file, CancellationToken cancellationToken = default);
    Task<OnnxModelStatusDto> SetActiveAsync(bool isActive, CancellationToken cancellationToken = default);
    Task<OnnxModelStatusDto> DeleteModelAsync(CancellationToken cancellationToken = default);
    float? Predict(RankingFeaturesDto features);
}
