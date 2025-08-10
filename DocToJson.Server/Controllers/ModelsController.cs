using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;

namespace DocToJson.Server.Controllers;

[ApiController]
[Route("api/models")]
public sealed class ModelsController(IConfiguration config) : ControllerBase
{
    public sealed record ModelRate(string Model, decimal InputPerM, decimal? OutputPerM, decimal? CachedInputPerM);

    [HttpGet]
    public ActionResult<string[]> Get()
    {
        var models = config
            .GetSection("OpenAI:ModelPricing:models")
            .Get<List<ModelRate>>()?
            .Select(m => m.Model)
            .ToList() ?? [];

        var aliases = config
            .GetSection("OpenAI:ModelPricing:aliases")
            .Get<Dictionary<string, string>>()?
            .Keys
            .ToList() ?? [];

        // Combine and distinct
        var allNames = models
            .Concat(aliases)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return Ok(allNames);
    }
}

