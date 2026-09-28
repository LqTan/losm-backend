namespace Users.Application.Abstractions;

public interface IAccountNotifier
{
    Task SendNewAccountAsync(
        NewAccountNotification notification,
        CancellationToken cancellationToken = default);

    Task SendPasswordChangedAsync(
        PasswordChangedNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record NewAccountNotification(
    string ToAddress,
    string FullName,
    string Username,
    string TemporaryPassword);

public sealed record PasswordChangedNotification(
    string ToAddress,
    string FullName,
    string Username);
