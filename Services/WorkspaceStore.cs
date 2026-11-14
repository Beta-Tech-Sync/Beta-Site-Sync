using System.Text.Json;
using SiteSyncTracker.Workspace;

namespace SiteSyncTracker.Services;

public sealed class WorkspaceStore
{
    private static readonly SemaphoreSlim Gate = new(1,1);
    private readonly string _path=Path.Combine(FileSystem.AppDataDirectory,"stock-workspace-v1.json");
    private readonly InventoryService _legacy;
    public WorkspaceStore(InventoryService legacy) => _legacy=legacy;
    public async Task<WorkspaceDocument> LoadAsync()
    {
        await Gate.WaitAsync();
        try { return await ReadAsync(); } finally { Gate.Release(); }
    }
    private async Task<WorkspaceDocument> ReadAsync()
    {
        if(File.Exists(_path))
        {
            var saved=JsonSerializer.Deserialize<WorkspaceDocument>(await File.ReadAllTextAsync(_path),WorkspaceRules.Json)!;
            WorkspaceRules.Validate(saved);return saved;
        }
        using var stream=await FileSystem.OpenAppPackageFileAsync("InventoryUi/catalog.json");
        var items=(await JsonSerializer.DeserializeAsync<List<StockItem>>(stream,WorkspaceRules.Json))!;
        await _legacy.InitializeAsync();
        foreach(var row in await _legacy.GetItemsAsync())
        {
            var sku=string.IsNullOrWhiteSpace(row.ManifestNumber) ? "LEGACY-"+row.Id : row.ManifestNumber.Trim();
            if(items.Any(i=>i.Sku.Equals(sku,StringComparison.OrdinalIgnoreCase))) sku="LEGACY-"+row.Id;
            items.Add(new StockItem {Sku=sku,Name=row.Description.Trim(),Category="Imported inventory",Unit="pcs",OpeningQuantity=row.QuantityReceived});
        }
        var doc=new WorkspaceDocument {Items=items};WorkspaceRules.Validate(doc);return doc;
    }
    public async Task<WorkspaceDocument> SaveAsync(WorkspaceDocument next)
    {
        await Gate.WaitAsync();
        try
        {
            var old=await ReadAsync();WorkspaceRules.Validate(next,old);
            var oldIds=old.Movements.Select(m=>m.Id).ToHashSet();
            foreach(var movement in next.Movements.Where(m=>!oldIds.Contains(m.Id)))movement.RecordedAt=DateTimeOffset.UtcNow;
            next.Revision++;
            var json=JsonSerializer.Serialize(next,WorkspaceRules.Json);
            if(json.Length>20_000_000)throw new ArgumentException("Workspace exceeds 20 MB.");
            var temporary=_path+".tmp";await File.WriteAllTextAsync(temporary,json);File.Move(temporary,_path,true);
            return next;
        }
        finally {Gate.Release();}
    }
}
