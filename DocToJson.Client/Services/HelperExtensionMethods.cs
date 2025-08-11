namespace DocToJson.Client.Services;

public static class HelperExtensionMethods
{
    public static string Prefix(this object? value, string prefix, string? format = null, IFormatProvider? provider = null)
        => value is null
            ? string.Empty
            : value is IFormattable f ? prefix + f.ToString(format, provider) : prefix + value;

    public static string Postfix(this object? value, string postfix, string? format = null, IFormatProvider? provider = null)
        => value is null
            ? string.Empty
            : value is IFormattable f ? f.ToString(format, provider) + postfix : value + postfix;

    // keep your string-specific overloads if you like; they'll be chosen over object?
    public static string Prefix(this string? value, string prefix)
        => !string.IsNullOrEmpty(value) ? prefix + value : string.Empty;

    public static string Postfix(this string? value, string postfix)
        => !string.IsNullOrEmpty(value) ? value + postfix : string.Empty;

    // ----- Join / EmptyIfNull -----
    public static string JoinToString<T>(this IEnumerable<T>? source, string separator)
        => source == null ? string.Empty : string.Join(separator, source);

    public static IEnumerable<T> EmptyIfNull<T>(this IEnumerable<T>? source)
        => source ?? Array.Empty<T>();

    // ----- Null/Empty -----
    public static bool IsNullOrEmpty<T>(this IEnumerable<T>? source)
        => source == null || !source.Any();

    public static bool IsNotNullOrEmpty<T>(this IEnumerable<T>? source)
        => source != null && source.Any();

    public static bool IsNullOrWhiteSpace(this string? s)
        => string.IsNullOrWhiteSpace(s);
}