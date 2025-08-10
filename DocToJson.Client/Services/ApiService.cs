using System.Net.Http.Json;
using DocToJson.Shared;

namespace DocToJson.Client.Services;


public sealed class ApiService(HttpClient http)
{
    public async Task<List<string>> GetModelsAsync(bool force = false, CancellationToken ct = default)
    {
        return (await http.GetFromJsonAsync<List<string>>("api/models", ct)) ?? [];
    }

    public async Task<DocumentExtractionResponse?> ExtractAsync(DocumentExtractionRequest payload, CancellationToken ct = default)
    {
        var res = await http.PostAsJsonAsync("api/extract", payload, ct);
        if (!res.IsSuccessStatusCode) return new DocumentExtractionResponse { IsError = true, Error = $"HTTP {(int)res.StatusCode} {res.ReasonPhrase}" };
        return await res.Content.ReadFromJsonAsync<DocumentExtractionResponse>(cancellationToken: ct);
    }
}