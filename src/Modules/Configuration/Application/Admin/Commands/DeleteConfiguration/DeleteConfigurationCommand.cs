namespace Configuration.Application.Admin.Commands.DeleteConfiguration;

public sealed record DeleteConfigurationCommand(
    string Scope,
    long ExpectedVersion,
    Guid? DeletedBy
);

public sealed record DeleteConfigurationResult(bool Succeeded, string? Error);
