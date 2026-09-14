using Configuration.Application.Models;

namespace Configuration.Application.Admin.Commands.UpsertConfiguration;

public sealed record UpsertConfigurationResult(
    bool Succeeded,
    long Version,
    string? Error
)
{
    public static UpsertConfigurationResult From(UpsertResult result) =>
        new(result.Succeeded, result.NewVersion, result.Error);
}
