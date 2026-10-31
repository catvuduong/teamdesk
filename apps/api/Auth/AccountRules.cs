namespace Api.Auth;

/// <summary>Input rules for accounts. Pure so they can be unit-tested without a database.</summary>
public static class AccountRules
{
    public const int MinPasswordLength = 8;
    public const int MaxPasswordLength = 200;
    public const int MaxNameLength = 100;
    public const int MaxEmailLength = 254;

    public static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    public static Dictionary<string, string> ValidateRegistration(string? name, string? email, string? password)
    {
        var errors = new Dictionary<string, string>();

        if (string.IsNullOrWhiteSpace(name)) errors["name"] = "Name is required";
        else if (name.Trim().Length > MaxNameLength) errors["name"] = $"Name must be {MaxNameLength} characters or fewer";

        var normalized = NormalizeEmail(email);
        if (normalized.Length == 0) errors["email"] = "Email is required";
        else if (normalized.Length > MaxEmailLength || !IsPlausibleEmail(normalized)) errors["email"] = "Enter a valid email address";

        if (string.IsNullOrEmpty(password)) errors["password"] = "Password is required";
        else if (password.Length < MinPasswordLength) errors["password"] = $"Password must be at least {MinPasswordLength} characters";
        else if (password.Length > MaxPasswordLength) errors["password"] = $"Password must be {MaxPasswordLength} characters or fewer";

        return errors;
    }

    public static bool IsPlausibleEmail(string email)
    {
        var at = email.IndexOf('@');
        return at > 0 && at < email.Length - 3 && email.IndexOf('.', at) > at + 1 && !email.Contains(' ');
    }
}
