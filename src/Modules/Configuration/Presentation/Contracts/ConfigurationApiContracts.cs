namespace Configuration.Presentation.Contracts;

public sealed record UpsertConfigurationApiRequest(
    string Module,
    string Key,
    object Value
);

public sealed record DeleteConfigurationApiRequest(long Version);
