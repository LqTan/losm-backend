namespace Configuration.Application.Models;

public sealed record UpsertResult(bool Succeeded, long NewVersion, string? Error);

public sealed record DeleteResult(bool Succeeded, string? Error);
