using System.Security.Claims;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SiteSyncTracker.Data;
using SiteSyncTracker.Workspace;

namespace SiteSyncTracker.Web;

public static class WorkspaceEndpoints
{
    public static void MapWorkspace(this WebApplication app)
    {
        app.MapGet("/api/workspace", async (ClaimsPrincipal user, AppDbContext db) =>
        {
            var owner = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var record = await db.Workspaces.AsNoTracking().SingleOrDefaultAsync(w=>w.OwnerId==owner);
            if (record is not null) return Results.Content(record.DataJson,"application/json");
            var path=Path.Combine(AppContext.BaseDirectory,"InventoryUi","catalog.json");
            var items=JsonSerializer.Deserialize<List<StockItem>>(await File.ReadAllTextAsync(path),WorkspaceRules.Json)!;
            return Results.Json(new WorkspaceDocument { Items=items },WorkspaceRules.Json);
        }).RequireAuthorization();

        app.MapPut("/api/workspace", async (WorkspaceDocument input, ClaimsPrincipal user, AppDbContext db) =>
        {
            var owner = user.FindFirstValue(ClaimTypes.NameIdentifier)!;
            var record=await db.Workspaces.SingleOrDefaultAsync(w=>w.OwnerId==owner);
            var previous=record is null ? new WorkspaceDocument() :
                JsonSerializer.Deserialize<WorkspaceDocument>(record.DataJson,WorkspaceRules.Json)!;
            try { WorkspaceRules.Validate(input,previous); }
            catch (InvalidOperationException e) { return Results.Problem(e.Message,statusCode:409); }
            catch (ArgumentException e) { return Results.Problem(e.Message,statusCode:400); }
            var existingIds=previous.Movements.Select(m=>m.Id).ToHashSet();
            foreach(var movement in input.Movements.Where(m=>!existingIds.Contains(m.Id))) movement.RecordedAt=DateTimeOffset.UtcNow;
            input.Revision=previous.Revision+1;
            var json=JsonSerializer.Serialize(input,WorkspaceRules.Json);
            if(json.Length>20_000_000) return Results.Problem("Workspace exceeds 20 MB.",statusCode:413);
            if(record is null) { record=new WorkspaceRecord {OwnerId=owner};db.Workspaces.Add(record); }
            record.DataJson=json;record.Revision=input.Revision;
            try { await db.SaveChangesAsync(); }
            catch (DbUpdateConcurrencyException) {return Results.Problem("Inventory changed. Reload before saving.",statusCode:409);}
            catch (DbUpdateException) {return Results.Problem("Inventory could not be saved. Reload before retrying.",statusCode:409);}
            return Results.Content(json,"application/json");
        }).RequireAuthorization();
    }
}
