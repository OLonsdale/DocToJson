using DocToJson.Client.Data;

namespace DocToJson.Client.Services;

using Blazored.LocalStorage;
using System.Text.Json;

public sealed class HistoryService(ILocalStorageService storage)
{
    const string Key = "pdfpoc_history";
    static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    readonly ILocalStorageService _storage = storage;
    List<HistoryEntry> _cache = [];

    public async Task<IReadOnlyList<HistoryEntry>> GetAllAsync()
    {
        if (_cache.Count > 0) return _cache;

        var json = await _storage.GetItemAsStringAsync(Key);
        _cache = string.IsNullOrWhiteSpace(json)
            ? []
            : (JsonSerializer.Deserialize<List<HistoryEntry>>(json, JsonOpts) ?? []);
        return _cache;
    }

    public async Task AddAsync(HistoryEntry entry)
    {
        await GetAllAsync();
        _cache.Insert(0, entry);
        await SaveAsync();
    }

    public async Task DeleteAsync(Guid id)
    {
        await GetAllAsync();
        var i = _cache.FindIndex(h => h.Id == id);
        if (i >= 0)
        {
            _cache.RemoveAt(i);
            await SaveAsync();
        }
    }

    public async Task ClearAsync()
    {
        _cache.Clear();
        await _storage.RemoveItemAsync(Key);
    }

    async Task SaveAsync()
    {
        var json = JsonSerializer.Serialize(_cache, JsonOpts);
        await _storage.SetItemAsStringAsync(Key, json);
    }
}