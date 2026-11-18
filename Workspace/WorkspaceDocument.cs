using System.Text.Json;

namespace SiteSyncTracker.Workspace;

public sealed class WorkspaceDocument
{
    public int Schema { get; set; } = 1;
    public int Revision { get; set; }
    public List<StockItem> Items { get; set; } = [];
    public List<GoodsMovement> Movements { get; set; } = [];
}
public sealed class StockItem
{
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
    public string Category { get; set; } = "";
    public string Unit { get; set; } = "pcs";
    public string Barcode { get; set; } = "";
    public decimal OpeningQuantity { get; set; }
    public decimal ReorderLevel { get; set; }
}
public sealed class GoodsMovement
{
    public string Id { get; set; } = "";
    public string Sku { get; set; } = "";
    public string Kind { get; set; } = "";
    public decimal Quantity { get; set; }
    public string Reference { get; set; } = "";
    public string Condition { get; set; } = "";
    public string Signer { get; set; } = "";
    public string Location { get; set; } = "";
    public Coordinates? Coordinates { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public string Notes { get; set; } = "";
    public bool Acknowledged { get; set; }
    public string Signature { get; set; } = "";
}
public sealed class Coordinates
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double Accuracy { get; set; }
}
public static class WorkspaceRules
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    public static void Validate(WorkspaceDocument document, WorkspaceDocument? previous = null)
    {
        if (document.Schema != 1 || document.Revision < 0 || document.Items is null || document.Movements is null
            || document.Items.Count > 5000 || document.Movements.Count > 10000)
            throw new ArgumentException("Unsupported inventory schema or size.");
        if (previous is not null && document.Revision != previous.Revision)
            throw new InvalidOperationException("Inventory changed. Reload before saving.");
        var codes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var balances = new Dictionary<string, decimal>(StringComparer.Ordinal);
        foreach (var item in document.Items)
        {
            Text(item.Sku, 50, "Item code"); Text(item.Name, 255, "Name");
            Text(item.Category, 80, "Category"); Text(item.Unit, 20, "Unit");
            if (item.Barcode is null || item.Barcode.Length > 200) throw new ArgumentException("Invalid barcode / NFC ID.");
            Quantity(item.OpeningQuantity); Quantity(item.ReorderLevel);
            if (!codes.Add(item.Sku)) throw new ArgumentException("Duplicate item code.");
            balances[item.Sku] = item.OpeningQuantity;
        }
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var movement in document.Movements)
        {
            Text(movement.Id,80,"Record ID"); Text(movement.Sku,50,"Item code");
            Text(movement.Reference,100,"Delivery / job reference"); Text(movement.Signer,120,"Signer");
            Text(movement.Location,255,"Location"); Quantity(movement.Quantity);
            if (movement.Quantity <= 0 || movement.Kind is not ("receive" or "issue")
                || movement.Condition is not ("accepted" or "short" or "damaged" or "quarantined")
                || (movement.Kind == "issue" && movement.Condition != "accepted")
                || movement.Notes is null || movement.Notes.Length > 1000
                || (movement.Condition != "accepted" && string.IsNullOrWhiteSpace(movement.Notes))
                || !ids.Add(movement.Id) || !balances.ContainsKey(movement.Sku))
                throw new ArgumentException("Invalid or duplicate goods record.");
            if (movement.OccurredAt == default || movement.RecordedAt == default)
                throw new ArgumentException("Goods and recording times are required.");
            if (!movement.Acknowledged || movement.Signature is null || !movement.Signature.StartsWith("data:image/png;base64,",StringComparison.Ordinal)
                || movement.Signature.Length is < 150 or > 150000)
                throw new ArgumentException("A signature and acknowledgement are required.");
            byte[] image;
            try { image = Convert.FromBase64String(movement.Signature[22..]); }
            catch (FormatException) { throw new ArgumentException("Invalid signature image."); }
            if (image.Length < 24 || !image.AsSpan(0,8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}))
                throw new ArgumentException("Signature must be a PNG image.");
            if (movement.Coordinates is { } gps && (!double.IsFinite(gps.Latitude) || Math.Abs(gps.Latitude)>90
                || !double.IsFinite(gps.Longitude) || Math.Abs(gps.Longitude)>180 || !double.IsFinite(gps.Accuracy) || gps.Accuracy<0))
                throw new ArgumentException("Invalid GPS coordinates.");
            balances[movement.Sku] += movement.Kind == "issue" ? -movement.Quantity :
                movement.Condition is "damaged" or "quarantined" ? 0 : movement.Quantity;
            if (balances[movement.Sku] < 0) throw new ArgumentException("Insufficient available stock.");
        }
        if (previous is not null)
        {
            var next = document.Movements.ToDictionary(m=>m.Id,StringComparer.Ordinal);
            foreach (var old in previous.Movements)
                if (!next.TryGetValue(old.Id,out var value) || JsonSerializer.Serialize(value,Json) != JsonSerializer.Serialize(old,Json))
                    throw new ArgumentException("Signed records cannot be changed or removed.");
            foreach (var old in previous.Items)
            {
                var item = document.Items.SingleOrDefault(i=>i.Sku==old.Sku)
                    ?? throw new ArgumentException("Existing stock cannot be removed by an import.");
                if (previous.Movements.Any(m=>m.Sku==old.Sku) && (item.OpeningQuantity!=old.OpeningQuantity || item.Unit!=old.Unit))
                    throw new ArgumentException("Opening quantity and unit are locked after a signed movement.");
            }
        }
    }
    private static void Text(string? value,int max,string label)
    { if (string.IsNullOrWhiteSpace(value) || value.Length>max || value!=value.Trim()) throw new ArgumentException(label+" is required and must be trimmed."); }
    private static void Quantity(decimal value)
    { if (value<0 || value>100000000 || decimal.Round(value,3)!=value) throw new ArgumentException("Use a non-negative quantity with up to 3 decimal places."); }
}
