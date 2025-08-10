using System.Net.Http.Json;
using DocToJson.Shared;

namespace DocToJson.Client.Services;


public sealed class ApiService(HttpClient http)
{
    public async Task<ModelsDto> GetModelsAsync(bool force = false, CancellationToken ct = default)
    {
        var url = $"api/models{(force ? "?force=true" : "")}";
        var dto = await http.GetFromJsonAsync<ModelsDto>(url, ct);
        return dto ?? new ModelsDto(DateTime.UtcNow, []);
    }

    public async Task<DocumentExtractionResponse?> ExtractAsync(DocumentExtractionRequest payload, CancellationToken ct = default)
    {
        var res = await http.PostAsJsonAsync("api/extract", payload, ct);
        if (!res.IsSuccessStatusCode) return new DocumentExtractionResponse { IsError = true, Error = $"HTTP {(int)res.StatusCode} {res.ReasonPhrase}" };
        return await res.Content.ReadFromJsonAsync<DocumentExtractionResponse>(cancellationToken: ct);
    }
}