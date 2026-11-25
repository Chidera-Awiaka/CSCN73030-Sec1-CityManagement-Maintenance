using System.Text.Json;

namespace Maintenance.Api.Infrastructure;

/// <summary>
/// Thread-safe list of records saved to a JSON file (or kept in memory when no file
/// is given, which is how the unit tests use it). Shared by the Sprint 3 services.
/// </summary>
/// <typeparam name="T">Record type.</typeparam>
public class JsonListStore<T>
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    private readonly string? _filePath;
    private readonly List<T> _items;

    /// <summary>Lock that callers hold while reading or changing <see cref="Items"/>.</summary>
    public object Sync { get; } = new();

    /// <summary>Creates the store and loads any saved records.</summary>
    /// <param name="filePath">JSON file; null keeps the data in memory only.</param>
    public JsonListStore(string? filePath)
    {
        _filePath = filePath;
        _items = Load();
    }

    /// <summary>The records. Only use while holding <see cref="Sync"/>.</summary>
    public List<T> Items => _items;

    /// <summary>Next numeric ID, given a selector for the existing IDs.</summary>
    public int NextId(Func<T, int> id) => _items.Count == 0 ? 1 : _items.Max(id) + 1;

    /// <summary>Writes the records to the JSON file (no-op in memory mode).</summary>
    public void Save()
    {
        if (_filePath is null) return;
        var folder = Path.GetDirectoryName(_filePath);
        if (!string.IsNullOrEmpty(folder)) Directory.CreateDirectory(folder);
        File.WriteAllText(_filePath, JsonSerializer.Serialize(_items, Options));
    }

    private List<T> Load()
    {
        if (_filePath is null || !File.Exists(_filePath)) return new List<T>();
        var json = File.ReadAllText(_filePath);
        return string.IsNullOrWhiteSpace(json)
            ? new List<T>()
            : JsonSerializer.Deserialize<List<T>>(json, Options) ?? new List<T>();
    }
}
