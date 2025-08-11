using System.Text.Json;

namespace DocToJson.Client.Services;

public static class HelperStaticMethods
{
    public static string PrettyJson(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return s ?? "";
        try
        {
            using var doc = JsonDocument.Parse(s);
            return JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            return s;
        }
    }
}