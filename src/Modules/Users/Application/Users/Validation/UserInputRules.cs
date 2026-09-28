using System.Text.RegularExpressions;
using Common.Application.Exceptions;
using Users.Domain.Enums;

namespace Users.Application.Users.Validation;

public static class UserInputRules
{
    public const int MinimumPasswordLength = 8;
    public const int MaximumPasswordLength = 128;
    public const int MaxFullNameLength = 255;

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

        if (!Regex.IsMatch(password, "[A-Z]"))
        {
            throw new ValidationException(
                "Password must contain at least 1 uppercase letter.");
        }

        if (!Regex.IsMatch(password, "[0-9]"))
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
            || !Regex.IsMatch(trimmed, @"^[^@\s]+@[^@\s]+\.[^@\s]{2,}$"))
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

    /// <summary>
    /// Vietnamese phone number: the country/local prefix (84 or 0) followed by
    /// 9 digits, e.g. 84397444937 or 0397444937.
    /// </summary>
    private const string PhonePattern = @"^(84|0)\d{9}$";

    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
            return null;

        var trimmed = phone.Trim();
        if (!Regex.IsMatch(trimmed, PhonePattern))
        {
            throw new ValidationException(
                "Phone number must be a Vietnamese number: 84 or 0 "
                + "followed by 9 digits (e.g. 84397444937 or 0397444937).");
        }

        return trimmed;
    }

    /// <summary>
    /// Personal name: letters of any script (so Vietnamese diacritics and
    /// minority names are accepted) plus the apostrophe used in names such as
    /// "A-Ma". Digits and other punctuation are rejected.
    /// </summary>
    private const string FullNamePattern = @"^[\p{L}']+(?: [\p{L}']+)*$";

    public static string RequireFullName(string? value)
    {
        var trimmed = RequireText(value, "Full name", MaxFullNameLength);

        if (!Regex.IsMatch(trimmed, FullNamePattern))
        {
            throw new ValidationException(
                "Full name must not contain digits or special characters. "
                + "Only letters and the apostrophe are allowed.");
        }

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
