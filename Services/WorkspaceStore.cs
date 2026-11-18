using System.Text.Json;
using Microsoft.Data.Sqlite;
using SiteSyncTracker.Workspace;

namespace SiteSyncTracker.Services;

public sealed class WorkspaceStore
{
    private const int MaximumDocumentLength = 20_000_000;
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private readonly InventoryService _legacy;
    private readonly string _legacyPath = Path.Combine(FileSystem.AppDataDirectory, "stock-workspace-v1.json");
    private readonly string _connectionString = new SqliteConnectionStringBuilder
    {
        DataSource = Path.Combine(FileSystem.AppDataDirectory, "stock-workspace.db")
    }.ToString();

    public WorkspaceStore(InventoryService legacy) => _legacy = legacy;

    public async Task<WorkspaceDocument> LoadAsync()
    {
        await Gate.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            return await LoadOrMigrateAsync(connection);
        }
        finally
        {
            Gate.Release();
        }
    }

    public async Task<WorkspaceDocument> SaveAsync(WorkspaceDocument next)
    {
        await Gate.WaitAsync();
        try
        {
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            var old = await LoadOrMigrateAsync(connection);
            WorkspaceRules.Validate(next, old);

            var oldIds = old.Movements.Select(movement => movement.Id).ToHashSet();
            foreach (var movement in next.Movements.Where(movement => !oldIds.Contains(movement.Id)))
                movement.RecordedAt = DateTimeOffset.UtcNow;

            next.Revision = checked(old.Revision + 1);
            using var command = connection.CreateCommand();
            command.CommandText = """
                UPDATE StockWorkspace
                SET Revision = $revision, Document = $document
                WHERE Id = 1 AND Revision = $previousRevision
                """;
            command.Parameters.AddWithValue("$revision", next.Revision);
            command.Parameters.AddWithValue("$document", Serialize(next));
            command.Parameters.AddWithValue("$previousRevision", old.Revision);
            if (await command.ExecuteNonQueryAsync() != 1)
                throw new InvalidOperationException("The stock workspace changed. Reload it before saving.");
            return next;
        }
        finally
        {
            Gate.Release();
        }
    }

    private async Task<WorkspaceDocument> LoadOrMigrateAsync(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = """
            CREATE TABLE IF NOT EXISTS StockWorkspace (
                Id INTEGER PRIMARY KEY CHECK (Id = 1),
                Revision INTEGER NOT NULL CHECK (Revision >= 0),
                Document TEXT NOT NULL
            )
            """;
        await command.ExecuteNonQueryAsync();

        command.CommandText = "SELECT Document FROM StockWorkspace WHERE Id = 1";
        if (await command.ExecuteScalarAsync() is string json)
            return Deserialize(json);

        var initial = File.Exists(_legacyPath)
            ? Deserialize(await File.ReadAllTextAsync(_legacyPath))
            : await CreateInitialWorkspaceAsync();
        command.CommandText = """
            INSERT INTO StockWorkspace (Id, Revision, Document)
            VALUES (1, $revision, $document)
            ON CONFLICT (Id) DO NOTHING
            """;
        command.Parameters.AddWithValue("$revision", initial.Revision);
        command.Parameters.AddWithValue("$document", Serialize(initial));
        if (await command.ExecuteNonQueryAsync() == 1)
            return initial;

        command.Parameters.Clear();
        command.CommandText = "SELECT Document FROM StockWorkspace WHERE Id = 1";
        return Deserialize((string)(await command.ExecuteScalarAsync()
            ?? throw new InvalidOperationException("The stock workspace could not be loaded.")));
    }

    private async Task<WorkspaceDocument> CreateInitialWorkspaceAsync()
    {
        using var stream = await FileSystem.OpenAppPackageFileAsync("InventoryUi/catalog.json");
        var items = await JsonSerializer.DeserializeAsync<List<StockItem>>(stream, WorkspaceRules.Json)
            ?? throw new InvalidOperationException("The stock catalogue is missing.");
        await _legacy.InitializeAsync();
        foreach (var row in await _legacy.GetItemsAsync())
        {
            var sku = string.IsNullOrWhiteSpace(row.ManifestNumber) ? "LEGACY-" + row.Id : row.ManifestNumber.Trim();
            if (items.Any(item => item.Sku.Equals(sku, StringComparison.OrdinalIgnoreCase)))
                sku = "LEGACY-" + row.Id;
            items.Add(new StockItem
            {
                Sku = sku,
                Name = row.Description.Trim(),
                Category = "Imported inventory",
                Unit = "pcs",
                OpeningQuantity = row.QuantityReceived
            });
        }

        var document = new WorkspaceDocument { Items = items };
        WorkspaceRules.Validate(document);
        return document;
    }

    private static WorkspaceDocument Deserialize(string json)
    {
        var document = JsonSerializer.Deserialize<WorkspaceDocument>(json, WorkspaceRules.Json)
            ?? throw new InvalidOperationException("The stock workspace is empty.");
        WorkspaceRules.Validate(document);
        return document;
    }

    private static string Serialize(WorkspaceDocument document)
    {
        var json = JsonSerializer.Serialize(document, WorkspaceRules.Json);
        if (json.Length > MaximumDocumentLength)
            throw new ArgumentException("Workspace exceeds 20 MB.");
        return json;
    }
}
