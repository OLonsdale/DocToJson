using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using DocToJson.Shared;
using Microsoft.AspNetCore.Mvc;

namespace DocToJson.Server.Controllers;

[ApiController]
[Route("api")]
public class ExtractionController(IHttpClientFactory httpClientFactory, IConfiguration config) : ControllerBase
{
    readonly IHttpClientFactory _http = httpClientFactory;
    readonly IConfiguration _cfg = config;

    static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    [HttpPost("extract")]
    public async Task<ActionResult<DocumentExtractionResponse>> Extract([FromBody] DocumentExtractionRequest request, CancellationToken ct)
    {
        try
        {
            if (request.Files is null || request.Files.Count == 0)
                return BadRequest(new DocumentExtractionResponse { IsError = true, Error = "No files provided." });

            var apiKey = _cfg["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
                return BadRequest(new DocumentExtractionResponse { IsError = true, Error = "OpenAI API key not configured." });

            var http = _http.CreateClient();
            http.Timeout = Timeout.InfiniteTimeSpan;
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            http.DefaultRequestHeaders.Accept.Clear();
            http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            // 1) upload each file -> file_id
            var fileIds = new List<string>(request.Files.Count);
            foreach (var f in request.Files)
            {
                using var mp = new MultipartFormDataContent();
                mp.Add(new ByteArrayContent(f.Bytes ?? []), "file", f.FileName ?? "file.bin");
                mp.Add(new StringContent("assistants"), "purpose");

                var upRes = await http.PostAsync(OpenAI.Files, mp, ct);
                var upJson = await upRes.Content.ReadAsStringAsync(ct);
                if (!upRes.IsSuccessStatusCode)
                    return StatusCode((int)upRes.StatusCode, new DocumentExtractionResponse { IsError = true, Error = upJson });

                var id = JsonDocument.Parse(upJson).RootElement.GetProperty("id").GetString();
                if (string.IsNullOrWhiteSpace(id))
                    return Problem("OpenAI did not return a file id.", statusCode: 502);

                fileIds.Add(id!);
            }

            ct.ThrowIfCancellationRequested();

            // 2) optional schema
            JsonElement? schemaEl = null;
            if (!string.IsNullOrWhiteSpace(request.JsonSchema))
            {
                try { schemaEl = JsonSerializer.Deserialize<JsonElement>(request.JsonSchema!, JsonOpts); }
                catch (JsonException jx)
                {
                    return BadRequest(new DocumentExtractionResponse { IsError = true, Error = $"Invalid JSON schema: {jx.Message}" });
                }
            }

            // 3) build payload: prompt + many input_file
            var content = new List<object>
            {
                new { type = "input_text", text = schemaEl is JsonElement ? "Output JSON only. Use the provided JSON schema." : "Output JSON only. Return a single JSON object." },
                new { type = "input_text", text = request.Prompt ?? "" }
            };
            content.AddRange(fileIds.Select(id => new { type = "input_file", file_id = id }));

            object payload =
                schemaEl is JsonElement s
                ? new
                {
                    model = OpenAI.Models.ResponsesModel,
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
                    model = OpenAI.Models.ResponsesModel,
                    input = new[] { new { role = "user", content } },
                    text = new { format = new { type = "json_object" } }
                };

            // 4) call responses
            using var msg = new HttpRequestMessage(HttpMethod.Post, OpenAI.Responses)
            {
                Content = new StringContent(JsonSerializer.Serialize(payload, JsonOpts), Encoding.UTF8, "application/json")
            };

            var res = await http.SendAsync(msg, ct);
            var resJson = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
                return StatusCode((int)res.StatusCode, new DocumentExtractionResponse { IsError = true, Error = resJson });

            var env = JsonSerializer.Deserialize<OpenAiResponseEnvelope>(resJson, JsonOpts) ?? new();
            if (env.Error is not null)
                return Ok(new DocumentExtractionResponse { IsError = true, Error = JsonSerializer.Serialize(env.Error, JsonOpts) });

            var text =
                env.Output?.FirstOrDefault()?.Content?.FirstOrDefault()?.Text
                ?? env.OutputText
                ?? string.Empty;

            return Ok(new DocumentExtractionResponse { IsError = false, Data = text });
        }
        catch (OperationCanceledException)
        {
            return StatusCode(499, new DocumentExtractionResponse { IsError = true, Error = "Request cancelled." });
        }
    }

    static class OpenAI
    {
        public static class Models
        {
            public const string ResponsesModel = "gpt-4.1-mini";
        }
        public static class Urls
        {
            public const string Base = "https://api.openai.com/v1";
            public const string Files = $"{Base}/files";
            public const string Responses = $"{Base}/responses";
        }
        public static string Files => Urls.Files;
        public static string Responses => Urls.Responses;
    }

    // minimal response envelope parsing
    sealed class OpenAiResponseEnvelope
    {
        public OpenAiError? Error { get; set; }
        public string? OutputText { get; set; }
        public List<OpenAiMessage>? Output { get; set; }
    }
    sealed class OpenAiError { public string? Message { get; set; } public string? Type { get; set; } public string? Code { get; set; } public string? Param { get; set; } }
    sealed class OpenAiMessage { public List<OpenAiContent>? Content { get; set; } }
    sealed class OpenAiContent { public string? Type { get; set; } public string? Text { get; set; } }
}
