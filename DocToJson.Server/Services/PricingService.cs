namespace DocToJson.Server.Services;

public sealed class PricingService
{
    public sealed record Rate(string Model, decimal InputPerM, decimal? OutputPerM, decimal? CachedInputPerM);

    // for config binding
    private sealed record ModelRate(string Model, decimal InputPerM, decimal? OutputPerM, decimal? CachedInputPerM);

    readonly Dictionary<string, Rate> _byModel = new(StringComparer.OrdinalIgnoreCase);
    readonly Dictionary<string, string> _aliases = new(StringComparer.OrdinalIgnoreCase);

    public PricingService(IConfiguration cfg)
    {
        var models = cfg.GetSection("OpenAI:ModelPricing:Models").Get<List<ModelRate>>() ?? [];
        var aliases = cfg.GetSection("OpenAI:ModelPricing:Aliases").Get<Dictionary<string, string>>() ?? new();

        foreach (var m in models)
            _byModel[m.Model] = new Rate(m.Model, m.InputPerM, m.OutputPerM, m.CachedInputPerM);

        foreach (var kv in aliases)
            _aliases[kv.Key] = kv.Value;
    }

    string Resolve(string model)
        => _byModel.ContainsKey(model)
            ? model
            : (_aliases.TryGetValue(model, out var baseId) ? baseId : model);

    public (decimal inPerM, decimal? outPerM, decimal? cachedInPerM) GetRates(string model)
    {
        var id = Resolve(model);
        return _byModel.TryGetValue(id, out var r)
            ? (r.InputPerM, r.OutputPerM, r.CachedInputPerM)
            : (0m, 0m, null);
    }

    public decimal EstimateUsd(string model, int inputTokens, int outputTokens, int cachedInputTokens = 0)
    {
        var (inPerM, outPerM, cachedPerM) = GetRates(model);
        if (inPerM == 0m && outPerM is null && cachedPerM is null) return 0m;

        var freshIn = Math.Max(0, inputTokens - Math.Max(0, cachedInputTokens));
        decimal cost = 0m;

        if (inPerM > 0m) cost += (freshIn / 1_000_000m) * inPerM;
        if (outPerM is decimal o) cost += (outputTokens / 1_000_000m) * o;
        if (cachedPerM is decimal c && cachedInputTokens > 0)
            cost += (cachedInputTokens / 1_000_000m) * c;

        return Math.Round(cost, 5, MidpointRounding.AwayFromZero);
    }
}