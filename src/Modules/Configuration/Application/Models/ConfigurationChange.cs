namespace Configuration.Application.Models;

public sealed record ConfigurationChange(
    string Scope,
    string? OldValueJson,
    string NewValueJson,
    long OldVersion,
    long NewVersion,
    DateTime ChangedAt,
    Guid? ChangedBy,
    string Action
);
