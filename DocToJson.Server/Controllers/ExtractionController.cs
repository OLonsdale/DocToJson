using System.Diagnostics;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocToJson.Server.Services;
using DocToJson.Shared;
using Microsoft.AspNetCore.Mvc;

namespace DocToJson.Server.Controllers;

[ApiController]
[Route("api/extract")]
public class ExtractionController(IHttpClientFactory httpClientFactory, IConfiguration config, PricingService pricing)
    : ControllerBase
{
    readonly IHttpClientFactory _http = httpClientFactory;
    readonly IConfiguration _cfg = config;

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [HttpPost]
    public async Task<ActionResult<DocumentExtractionResponse>> Extract([FromBody] DocumentExtractionRequest request, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();

        try
        {
            if (request.Files.Count == 0)
                return BadRequest(new DocumentExtractionResponse { IsError = true, Error = "No files provided." });

            var apiKey = _cfg["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return BadRequest(new DocumentExtractionResponse { IsError = true, Error = "OpenAI API key not configured." });

            var http = _http.CreateClient("openai");
            http.Timeout = Timeout.InfiniteTimeSpan;
            http.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", apiKey);
            http.DefaultRequestHeaders.Accept.Clear();
            http.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));

            // 1) Upload files
            var fileIds = new List<string>(request.Files.Count);
            var filesOut = new List<FileProvenance>(request.Files.Count);

            foreach (var f in request.Files)
            {
                using var mp = new MultipartFormDataContent();
                mp.Add(new ByteArrayContent(f.Bytes), "file", f.FileName);
                mp.Add(new StringContent("assistants"), "purpose");

                var upRes = await http.PostAsync(OpenAI.Files, mp, ct);
                var upJson = await upRes.Content.ReadAsStringAsync(ct);
                if (!upRes.IsSuccessStatusCode)
                    return StatusCode((int)upRes.StatusCode, new DocumentExtractionResponse { IsError = true, Error = upJson });

                var id = JsonDocument.Parse(upJson).RootElement.GetProperty("id").GetString();
                if (string.IsNullOrWhiteSpace(id))
                    return Problem("OpenAI did not return a file id.", statusCode: 502);

                fileIds.Add(id);
                filesOut.Add(new FileProvenance(
                    FileName: f.FileName,
                    FileId: id,
                    SizeBytes: f.Bytes.LongLength,
                    ContentType: null
                ));
            }

            ct.ThrowIfCancellationRequested();

            // 2) Optional schema
            JsonElement? schemaEl = null;
            if (!string.IsNullOrWhiteSpace(request.JsonSchema))
            {
                try { schemaEl = JsonSerializer.Deserialize<JsonElement>(request.JsonSchema!, JsonOpts); }
                catch (JsonException jx)
                {
                    return BadRequest(new DocumentExtractionResponse { IsError = true, Error = $"Invalid JSON schema: {jx.Message}" });
                }
            }

            // 3) Build content for Responses API
            var content = new List<object>
            {
                new { type = "input_text", text = schemaEl != null ? "Output JSON only. Use the provided JSON schema." : "Output JSON only. Return a single JSON object." },
                new { type = "input_text", text = request.Prompt }
            };
            content.AddRange(fileIds.Select(id => new { type = "input_file", file_id = id }));

            // Always use the model string the client sent (or default)
            var requestedModel = request.Model ?? _cfg["OpenAI:DefaultModel"] ?? "gpt-4.1-mini";

            object payload =
                schemaEl is { } s
                ? new
                {
                    model = requestedModel,
                    input = new[] { new { role = "user", content } },
                    text = new
                    {
                        format = new
                        {
                            type = "json_schema",
                            name = string.IsNullOrWhiteSpace(request.SchemaName) ? "schema" : request.SchemaName!,
                            strict = true,
                            schema = s
                        }
                    }
                }
                : new
                {
                    model = requestedModel,
                    input = new[] { new { role = "user", content } },
                    text = new { format = new { type = "json_object" } }
                };

            // 4) Call Responses API
            using var msg = new HttpRequestMessage(HttpMethod.Post, OpenAI.Responses);
            msg.Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json");

            var res = await http.SendAsync(msg, ct);
            var resJson = await res.Content.ReadAsStringAsync(ct);

            sw.Stop();
            var latencyMs = (int)sw.ElapsedMilliseconds;

            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode, new DocumentExtractionResponse { IsError = true, Error = resJson });

            // 5) Parse response strongly-typed
            var parsed = JsonSerializer.Deserialize<OpenAiResponsesEnvelope>(resJson, JsonOpts);

            if (parsed?.Error is not null)
                return Ok(new DocumentExtractionResponse { IsError = true, Error = JsonSerializer.Serialize(parsed.Error, JsonOpts) });

            var text =
                parsed?.OutputText?.FirstOrDefault()
                ?? parsed?.Output?
                    .SelectMany(o => o.Content ?? [])
                    .Select(c => c.Text)
                    .FirstOrDefault(t => !string.IsNullOrWhiteSpace(t))
                ?? string.Empty;

            var inTok = parsed?.Usage?.InputTokens ?? 0;
            var outTok = parsed?.Usage?.OutputTokens ?? 0;
            var totalTok = parsed?.Usage?.TotalTokens ?? (inTok + outTok);

            // 6) Price using the requested model (not the server-returned model name)
            var usd = pricing.EstimateUsd(requestedModel, inTok, outTok);

            return Ok(new DocumentExtractionResponse
            {
                IsError = false,
                Data = text,
                Details = new RunDetails
                {
                    Model = requestedModel,
                    InputTokens = inTok,
                    OutputTokens = outTok,
                    TotalTokens = totalTok,
                    LatencyMs = latencyMs,
                    EstimatedCostUsd = usd
                },
                Usage = new Usage(inTok, outTok, totalTok, ReasoningTokens: null),
                Files = filesOut.ToArray()
            });
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested && sw.Elapsed.TotalSeconds >= 90)
        {
            // Treat as OpenAI timeout (client didn't cancel; elapsed is long)
            return StatusCode(504, new DocumentExtractionResponse
            {
                IsError = true,
                Error = "Timed out by OpenAI."
            });
        }
        catch (OperationCanceledException)
        {
            // Client-side cancel
            return StatusCode(499, new DocumentExtractionResponse
            {
                IsError = true,
                Error = "Request cancelled."
            });
        }
    }

    static class OpenAI
    {
        public const string Files = "files";
        public const string Responses = "responses";
    }
    

    sealed class OpenAiResponsesEnvelope
    {
        [JsonPropertyName("error")]
        public OpenAiError? Error { get; set; }

        [JsonPropertyName("usage")]
        public OpenAiUsage? Usage { get; set; }

        // "output_text": [ "..." ]
        [JsonPropertyName("output_text")]
        public List<string>? OutputText { get; set; }

        // "output": [ { content: [ { type, text } ] } ]
        [JsonPropertyName("output")]
        public List<OpenAiOutput>? Output { get; set; }
    }

    sealed class OpenAiError
    {
        [JsonPropertyName("message")] public string? Message { get; set; }
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("code")] public string? Code { get; set; }
        [JsonPropertyName("param")] public string? Param { get; set; }
    }

    sealed class OpenAiUsage
    {
        [JsonPropertyName("input_tokens")] public int InputTokens { get; set; }
        [JsonPropertyName("output_tokens")] public int OutputTokens { get; set; }
        [JsonPropertyName("total_tokens")] public int TotalTokens { get; set; }
    }

    sealed class OpenAiOutput
    {
        [JsonPropertyName("content")]
        public List<OpenAiContent>? Content { get; set; }
    }

    sealed class OpenAiContent
    {
        [JsonPropertyName("type")] public string? Type { get; set; }
        [JsonPropertyName("text")] public string? Text { get; set; }
    }
}
