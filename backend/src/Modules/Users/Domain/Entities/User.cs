using System.Text.Json.Serialization;

namespace Users.Domain.Entities;

[JsonConverter(typeof(FlexibleUserRoleConverter))]
public enum UserRole
{
    User = 0,
    Admin = 1
}

public class User
{
    public Guid Id { get; private set; }
    public string Username { get; private set; } = null!;
    public string Email { get; private set; } = null!;
    public string PasswordHash { get; private set; } = null!;
    public UserRole Role { get; private set; } = UserRole.User;
    public string? AvatarUrl { get; private set; }
    public DateTime CreatedAt { get; private set; }

    private User(){}

    public User(
        Guid id,
        string username,
        string email,
        string passwordHash,
        UserRole role = UserRole.User,
        string? avatarUrl = null
    )
    {
        Id = id;
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        AvatarUrl = avatarUrl;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateProfile(string username)
    {
        Username = username;
    }

    public void UpdateAvatar(string? avatarUrl)
    {
        AvatarUrl = avatarUrl;
    }

    public void SetRole(UserRole role)
    {
        Role = role;
    }

    public void UpdateAdmin(string username, string email, UserRole role, string? newPasswordHash = null)
    {
        Username = username;
        Email = email;
        Role = role;
        if (!string.IsNullOrWhiteSpace(newPasswordHash))
        {
            PasswordHash = newPasswordHash;
        }
    }
}