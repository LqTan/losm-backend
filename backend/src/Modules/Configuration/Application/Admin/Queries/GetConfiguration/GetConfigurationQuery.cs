namespace Configuration.Application.Admin.Queries.GetConfiguration;

public sealed record GetConfigurationQuery(string Scope);

public sealed record GetConfigurationResult(
    string Scope,
    string Module,
    string Key,
    string ValueJson,
    long Version,
    DateTime UpdatedAt,
    Guid? UpdatedBy
);
