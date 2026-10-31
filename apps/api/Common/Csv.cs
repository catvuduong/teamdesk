namespace Api;

/// <summary>RFC 4180 CSV escaping. Pure: unit-tested without a database.</summary>
public static class Csv
{
    public const string NewLine = "\r\n";

    private static readonly char[] SpecialCharacters = [',', '"', '\r', '\n'];

    /// <summary>A field is wrapped in quotes when it holds a comma, a quote or a line break; quotes inside are doubled.</summary>
    public static string Field(string? value)
    {
        if (string.IsNullOrEmpty(value)) return "";
        if (value.IndexOfAny(SpecialCharacters) < 0) return value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    public static string Line(IEnumerable<string?> fields) => string.Join(',', fields.Select(Field));
}
