using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DocToJson.Server.Controllers;

[ApiController]
[Route("api/models")]
public class ModelsController(IMemoryCache cache, IHttpClientFactory httpFactory, IConfiguration config) : ControllerBase
{
    static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);
    const string CacheKey = "openai_models";
    static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);

    [HttpGet]
    public async Task<ActionResult<List<string>>> Get([FromQuery] bool force = false, CancellationToken ct = default)
    {

        return new(
        [
            "chatgpt-4o-latest",
            "gpt-4.1",
            "gpt-4.1-2025-04-14",
            "gpt-4.1-mini",
            "gpt-4.1-mini-2025-04-14",
            "gpt-4.1-nano",
            "gpt-4.1-nano-2025-04-14",
            "gpt-4o",
            "gpt-4o-2024-08-06",
            "gpt-4o-2024-11-20",
            "gpt-4o-mini",
            "gpt-4o-mini-2024-07-18"
        ]);
        
        // below gets all from OpenAI API, but we hardcode the ones that actually support JSON schema extraction
        
        if (!force && cache.TryGetValue(CacheKey, out ModelsDto cached))
            return Ok(cached);

        var apiKey = config["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return StatusCode(500, new { error = "Missing OpenAI:ApiKey" });

        var client = httpFactory.CreateClient("openai");
        using var req = new HttpRequestMessage(HttpMethod.Get, "models");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        using var res = await client.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            return StatusCode(502, new { status = (int)res.StatusCode, error = body });

        var parsed = JsonSerializer.Deserialize<OpenAIListResponse>(body, JsonOpts);
        var ids = parsed?.data?.Select(m => m.id).Where(s => !string.IsNullOrWhiteSpace(s)).Distinct().OrderBy(s => s).ToArray() ?? [];

        var dto = new ModelsDto(DateTime.UtcNow, ids);
        cache.Set(CacheKey, dto, Ttl);
        return Ok(dto);
    }

    public sealed record OpenAIListResponse(OpenAIModel[] data);
    public sealed record OpenAIModel(string id, string? owned_by);
    public sealed record ModelsDto(DateTime fetchedAt, string[] models);
}