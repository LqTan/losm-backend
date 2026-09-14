namespace Configuration.Application.Models;

public sealed record ConfigurationDoc(
    string Scope,
    string Module,
    string Key,
    string ValueJson,
    long Version,
    DateTime UpdatedAt,
    Guid? UpdatedBy
);
