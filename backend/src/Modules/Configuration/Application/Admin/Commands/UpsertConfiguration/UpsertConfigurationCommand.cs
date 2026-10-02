namespace Configuration.Application.Admin.Commands.UpsertConfiguration;

public sealed record UpsertConfigurationCommand(
    string Scope,
    string Module,
    string Key,
    string ValueJson,
    long? ExpectedVersion,
    Guid? UpdatedBy
);
