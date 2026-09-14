namespace Configuration.Application.Admin.Queries.ListConfigurations;

public sealed record ListConfigurationsResult(
    IReadOnlyList<string> Scopes,
    long CurrentVersion
);
