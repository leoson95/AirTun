namespace AirTun.Core;

public static class PinCode
{
    public const int Length = 4;
    public const int Min = 1000;
    public const int Max = 9999;

    public static bool IsValid(string? input)
    {
        var normalized = Normalize(input);
        return normalized is not null && normalized.Length == Length;
    }

    public static string? Normalize(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var digits = new string(input.Where(ch => !char.IsWhiteSpace(ch)).ToArray());
        if (digits.Length != Length) return null;
        if (!digits.All(char.IsAsciiDigit)) return null;
        return digits;
    }
}
