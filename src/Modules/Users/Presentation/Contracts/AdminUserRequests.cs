namespace Users.Presentation.Contracts;

public sealed record CreateUserRequest(
    string Username,
    string Email,
    string FullName,
    string? Phone,
    string? Gender,
    string Role,
    string Status,
    string Password,
    bool Notify);

public sealed record UpdateUserRequest(
    string Username,
    string Email,
    string FullName,
    string? Phone,
    string? Gender,
    string Role,
    string Status,
    bool Notify);

public sealed record ResetUserPasswordRequest(
    string Password,
    bool Notify);
