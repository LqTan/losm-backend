namespace Users.Domain.Entities;

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
    public DateTime CreatedAt { get; private set; }

    private User(){}

    public User(
        Guid id,
        string username,
        string email,
        string passwordHash,
        UserRole role = UserRole.User
    )
    {
        Id = id;
        Username = username;
        Email = email;
        PasswordHash = passwordHash;
        Role = role;
        CreatedAt = DateTime.UtcNow;
    }

    public void UpdateProfile(string username)
    {
        Username = username;
    }

    public void SetRole(UserRole role)
    {
        Role = role;
    }
}