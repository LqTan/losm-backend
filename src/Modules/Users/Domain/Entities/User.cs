using Users.Domain.Enums;

namespace Users.Domain.Entities;

public class User
{
    public Guid Id { get; private set; }
    public string Username { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public string FullName { get; private set; } = null!;
    public string? Phone { get; private set; }
    public UserGender? Gender { get; private set; }
    public UserRole Role { get; private set; }
    public UserStatus Status { get; private set; }
    public bool NotifyOnAccountCreation { get; private set; }
    public DateTime? LastLoginAt { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime ChangedAt { get; private set; }

    private User() {}

    /// <summary>
    /// Self-service registration: a brand new customer account.
    /// </summary>
    public User(
        Guid id,
        string username,
        string email,
        string passwordHash
    ) : this(
        id,
        username,
        email,
        passwordHash,
        username,
        null,
        null,
        UserRole.Customer,
        UserStatus.Active,
        false
    )
    {
    }

    public User(
        Guid id,
        string username,
        string email,
        string passwordHash,
        string fullName,
        string? phone,
        UserGender? gender,
        UserRole role,
        UserStatus status,
        bool notifyOnAccountCreation
    )
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException(
                "Username is required.",
                nameof(username));
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException(
                "Email is required.",
                nameof(email));
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException(
                "Password hash is required.",
                nameof(passwordHash));
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException(
                "Full name is required.",
                nameof(fullName));

        Id = id;
        Username = username.Trim();
        Email = email.Trim();
        PasswordHash = passwordHash;
        FullName = fullName.Trim();
        Phone = NormalizePhone(phone);
        Gender = gender;
        Role = role;
        Status = status;
        NotifyOnAccountCreation = notifyOnAccountCreation;
        CreatedAt = DateTime.UtcNow;
        ChangedAt = CreatedAt;
    }

    public void UpdateProfile(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
            throw new ArgumentException(
                "Username is required.",
                nameof(username));

        Username = username.Trim();
        Touch();
    }

    public void UpdateDetails(
        string fullName,
        string? phone,
        UserGender? gender,
        UserRole role,
        UserStatus status,
        bool notifyOnAccountCreation
    )
    {
        if (string.IsNullOrWhiteSpace(fullName))
            throw new ArgumentException(
                "Full name is required.",
                nameof(fullName));

        FullName = fullName.Trim();
        Phone = NormalizePhone(phone);
        Gender = gender;
        Role = role;
        Status = status;
        NotifyOnAccountCreation = notifyOnAccountCreation;
        Touch();
    }

    public void UpdateContact(
        string email,
        string? phone,
        UserGender? gender
    )
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException(
                "Email is required.",
                nameof(email));

        Email = email.Trim();
        Phone = NormalizePhone(phone);
        Gender = gender;
        Touch();
    }

    public void ChangePassword(string passwordHash)
    {
        if (string.IsNullOrWhiteSpace(passwordHash))
            throw new ArgumentException(
                "Password hash is required.",
                nameof(passwordHash));

        PasswordHash = passwordHash;
        Touch();
    }

    public void RecordLogin()
    {
        LastLoginAt = DateTime.UtcNow;
    }

    public bool IsAdministrator => Role == UserRole.Administrator;

    private void Touch() => ChangedAt = DateTime.UtcNow;

    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var trimmed = phone.Trim();
        return trimmed.Length == 0 ? null : trimmed;
    }
}
