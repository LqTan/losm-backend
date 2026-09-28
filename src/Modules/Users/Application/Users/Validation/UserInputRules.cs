using Common.Application.Exceptions;
using Users.Domain.Enums;

namespace Users.Application.Users.Validation;

public static class UserInputRules
{
    public const int MinimumPasswordLength = 8;
    public const int MaximumPasswordLength = 128;

    public static void ValidatePassword(string? password)
    {
        if (string.IsNullOrWhiteSpace(password))
            throw new ValidationException("Password is required.");

        if (password.Length < MinimumPasswordLength)
            throw new ValidationException(
                "Password must be at least "
                + MinimumPasswordLength
                + " characters long.");

        if (password.Length > MaximumPasswordLength)
            throw new ValidationException(
                "Password must be at most "
                + MaximumPasswordLength
                + " characters long.");

        if (!System.Text.RegularExpressions.Regex.IsMatch(
            password, "[A-Z]"))
        {
            throw new ValidationException(
                "Password must contain at least 1 uppercase letter.");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(
            password, "[0-9]"))
        {
            throw new ValidationException(
                "Password must contain at least 1 number.");
        }
    }

    public static string RequireEmail(string? email, string field = "Email")
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ValidationException($"{field} is required.");

        var trimmed = email.Trim();
        if (trimmed.Length > 255
            || !System.Text.RegularExpressions.Regex.IsMatch(
                trimmed,
                @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$",
                System.Text.RegularExpressions.RegexOptions.None,
                TimeSpan.FromSeconds(1)))
        {
            throw new ValidationException(
                $"{field} is not a valid email address.");
        }

        return trimmed;
    }

    public static string RequireText(
        string? value,
        string field,
        int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ValidationException($"{field} is required.");

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
            throw new ValidationException(
                $"{field} must be at most {maxLength} characters.");

        return trimmed;
    }

    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var trimmed = phone.Trim();
        if (!System.Text.RegularExpressions.Regex.IsMatch(
            trimmed, @"^\d+$"))
        {
            throw new ValidationException(
                "Phone number must contain digits only.");
        }

        if (trimmed.Length is < 9 or > 15)
            throw new ValidationException(
                "Phone number must be between 9 and 15 digits.");

        return trimmed;
    }

    public static UserRole ParseRole(string? value)
    {
        if (!Enum.TryParse<UserRole>(value, ignoreCase: true, out var role))
            throw new ValidationException(
                "Role must be either Administrator or Customer.");

        return role;
    }

    public static UserStatus ParseStatus(string? value)
    {
        if (!Enum.TryParse<UserStatus>(value, ignoreCase: true, out var status))
            throw new ValidationException(
                "Status must be either Active or Blocked.");

        return status;
    }

    public static UserGender? ParseGender(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        if (!Enum.TryParse<UserGender>(value, ignoreCase: true, out var gender))
            throw new ValidationException(
                "Gender must be Male, Female or Others.");

        return gender;
    }
}
