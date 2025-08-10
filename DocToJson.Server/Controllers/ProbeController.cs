using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Mvc;

namespace DocToJson.Server.Controllers;

[ApiController]
[Route("api/dev/probe-models")]
public sealed class ProbeController(
    IConfiguration config,
    IHttpClientFactory httpFactory,
    IWebHostEnvironment env) : ControllerBase
{
    static readonly JsonSerializerOptions J = new(JsonSerializerDefaults.Web);

    [HttpGet]
    public async Task<IActionResult> Get([FromQuery] int timeoutSeconds = 20, CancellationToken ct = default)
    {
        if (!env.IsDevelopment()) return NotFound();

        var apiKey = config["OpenAI:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
            return StatusCode(500, new { error = "Missing OpenAI:ApiKey" });

        var http = httpFactory.CreateClient("openai");
        http.Timeout = TimeSpan.FromSeconds(Math.Clamp(timeoutSeconds, 5, 120));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

        var modelsJson = await http.GetStringAsync("models", ct);
        var modelsNode = JsonNode.Parse(modelsJson)!.AsObject();
        var allIds = modelsNode["data"]!.AsArray()
            .Select(n => n!["id"]!.GetValue<string>())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        bool Relevant(string id) =>
            id.Contains("gpt", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("o1", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("o3", StringComparison.OrdinalIgnoreCase) ||
            id.Contains("o4", StringComparison.OrdinalIgnoreCase);

        bool NotObviouslyIrrelevant(string id) =>
            !id.Contains("embedding", StringComparison.OrdinalIgnoreCase) &&
            !id.Contains("audio", StringComparison.OrdinalIgnoreCase) &&
            !id.Contains("tts", StringComparison.OrdinalIgnoreCase) &&
            !id.Contains("dall", StringComparison.OrdinalIgnoreCase) &&
            !id.Contains("realtime", StringComparison.OrdinalIgnoreCase);

        var candidates = allIds.Where(id => Relevant(id) && NotObviouslyIrrelevant(id)).ToList();

        var testSchema = new
        {
            type = "object",
            properties = new { ok = new { type = "integer" } },
            required = new[] { "ok" },
            additionalProperties = false
        };

        var results = new List<object>();
        var supported = new List<string>();

        foreach (var model in candidates)
        {
            var payload = new
            {
                model,
                input = "Return ok: 1.",
                text = new
                {
                    format = new
                    {
                        type = "json_schema",
                        name = "Probe",
                        schema = testSchema,
                        strict = true
                    }
                },
                temperature = 0
            };

            using var req = new HttpRequestMessage(HttpMethod.Post, "responses")
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, J), Encoding.UTF8, "application/json")
            };

            try
            {
                using var res = await http.SendAsync(req, ct);
                var body = await res.Content.ReadAsStringAsync(ct);

                if (!res.IsSuccessStatusCode)
                {
                    results.Add(new
                    {
                        model,
                        ok = false,
                        status = (int)res.StatusCode,
                        note = body.Length > 300 ? body[..300] + "…" : body
                    });
                    continue;
                }

                static string? ExtractFirstText(JsonDocument d)
                {
                    if (d.RootElement.TryGetProperty("output_text", out var ot) && ot.ValueKind == JsonValueKind.Array)
                        return ot.EnumerateArray().FirstOrDefault().GetString();

                    if (d.RootElement.TryGetProperty("output", out var outArr) && outArr.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in outArr.EnumerateArray())
                        {
                            if (item.ValueKind != JsonValueKind.Object) continue;
                            if (!item.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) continue;
                            foreach (var c in content.EnumerateArray())
                            {
                                if (c.ValueKind != JsonValueKind.Object) continue;
                                if (c.TryGetProperty("text", out var txt) && txt.ValueKind == JsonValueKind.String)
                                    return txt.GetString();
                            }
                        }
                    }
                    return null;
                }

                using var doc = JsonDocument.Parse(body);
                var text = ExtractFirstText(doc);
                bool ok = false;

                if (!string.IsNullOrWhiteSpace(text))
                {
                    try
                    {
                        using var parsed = JsonDocument.Parse(text);
                        ok = parsed.RootElement.TryGetProperty("ok", out var okEl) && okEl.GetInt32() == 1;
                    }
                    catch
                    {
                        ok = false;
                    }
                }

                if (ok) supported.Add(model);

                results.Add(new
                {
                    model,
                    ok,
                    status = (int)HttpStatusCode.OK,
                    note = ok ? null : "Schema parse mismatch or no text"
                });
            }
            catch (Exception ex)
            {
                results.Add(new { model, ok = false, status = 0, note = ex.Message });
            }
        }

        var response = new
        {
            fetchedAt = DateTime.UtcNow,
            candidates,
            supported = supported.OrderBy(s => s, StringComparer.OrdinalIgnoreCase).ToArray(),
            details = results
        };

        return Ok(response);
    }
}
