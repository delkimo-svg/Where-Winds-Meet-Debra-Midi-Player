using WhereWindsMeetMidiPlayer.Infrastructure;
using WhereWindsMeetMidiPlayer.Models;

namespace WhereWindsMeetMidiPlayer.Services;

public sealed class HistoryService
{
    private readonly List<HistoryItem> _items = [];

    public IReadOnlyList<HistoryItem> Items => _items;

    public void Clear()
    {
        _items.Clear();
    }

    public void Record(HistoryItem item)
    {
        var existing = _items.FindIndex(i => i.Id == item.Id);
        if (existing >= 0)
            _items.RemoveAt(existing);

        _items.Insert(0, item);
        if (_items.Count > 500)
            _items.RemoveRange(500, _items.Count - 500);
    }

    public void Save() => JsonFileStore.Write(AppPaths.HistoryFile, _items);

    public async Task SaveAsync(CancellationToken cancellationToken = default) =>
        await JsonFileStore.WriteAsync(AppPaths.HistoryFile, _items, cancellationToken);

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        var loaded = await JsonFileStore.ReadAsync<List<HistoryItem>>(AppPaths.HistoryFile, cancellationToken);
        _items.Clear();
        if (loaded is not null)
            _items.AddRange(loaded.OrderByDescending(h => h.PlayedAt));
    }

    public void Load()
    {
        var loaded = JsonFileStore.Read<List<HistoryItem>>(AppPaths.HistoryFile);
        _items.Clear();
        if (loaded is not null)
            _items.AddRange(loaded.OrderByDescending(h => h.PlayedAt));
    }
}
