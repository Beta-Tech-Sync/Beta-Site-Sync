using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using SiteSyncTracker.Data;
using SiteSyncTracker.Models;
using SiteSyncTracker.Web;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Inventory") ?? GetDatabaseConnectionString("sitesync.db")));
builder.Services.AddDbContext<AuthDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Identity") ?? GetDatabaseConnectionString("sitesync-auth.db")));
builder.Services
    .AddIdentityApiEndpoints<IdentityUser>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 12;
        options.Password.RequireDigit = true;
        options.Password.RequireNonAlphanumeric = true;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
    })
    .AddRoles<IdentityRole>()
    .AddEntityFrameworkStores<AuthDbContext>();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddProblemDetails();
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // Keep the framework defaults: only trusted loopback proxies are accepted.
});
builder.Services.AddRateLimiter(options =>
    options.AddFixedWindowLimiter("auth", limiterOptions =>
    {
        limiterOptions.PermitLimit = 5;
        limiterOptions.Window = TimeSpan.FromMinutes(1);
        limiterOptions.QueueLimit = 0;
    }));

var app = builder.Build();

app.UseExceptionHandler();
app.UseForwardedHeaders();
if (!builder.Configuration.GetValue<bool>("DisableHttpsRedirection"))
{
    app.UseHttpsRedirection();
}
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseStaticFiles(new StaticFileOptions {
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(Path.Combine(AppContext.BaseDirectory, "InventoryUi")),
    RequestPath = "/workspace"
});
app.MapGet("/workspace", () => Results.Redirect("/workspace/index.html"));
app.MapWorkspace();
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

using (var scope = app.Services.CreateScope())
{
    var database = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (app.Environment.IsEnvironment("Testing"))
    {
        await database.Database.EnsureCreatedAsync();
    }
    else
    {
        await database.Database.MigrateAsync();
    }

    var authDatabase = scope.ServiceProvider.GetRequiredService<AuthDbContext>();
    if (app.Environment.IsEnvironment("Testing"))
    {
        await authDatabase.Database.EnsureCreatedAsync();
    }
    else
    {
        await authDatabase.Database.MigrateAsync();
    }

    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Admin", "User" })
    {
        if (!await roleManager.RoleExistsAsync(role))
        {
            var result = await roleManager.CreateAsync(new IdentityRole(role));
            if (!result.Succeeded)
            {
                throw new InvalidOperationException($"Unable to create role '{role}'.");
            }
        }
    }

    var adminEmail = builder.Configuration["Admin:Email"];
    var adminPassword = builder.Configuration["Admin:Password"];
    if (!string.IsNullOrWhiteSpace(adminEmail) && !string.IsNullOrWhiteSpace(adminPassword))
    {
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();
        var admin = await userManager.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new IdentityUser { UserName = adminEmail, Email = adminEmail, EmailConfirmed = true };
            var result = await userManager.CreateAsync(admin, adminPassword);
            if (!result.Succeeded)
            {
                throw new InvalidOperationException("Unable to create the configured administrator.");
            }
        }

        if (!await userManager.IsInRoleAsync(admin, "Admin"))
        {
            await userManager.AddToRoleAsync(admin, "Admin");
        }
    }
}

app.MapIdentityApi<IdentityUser>().RequireRateLimiting("auth");

app.MapPost("/api/auth/register", async (RegisterRequest request, UserManager<IdentityUser> userManager) =>
{
    var user = new IdentityUser
    {
        UserName = request.Email,
        Email = request.Email
    };

    var result = await userManager.CreateAsync(user, request.Password);
    if (!result.Succeeded)
    {
        return Results.ValidationProblem(result.Errors
            .GroupBy(error => error.Code)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Description).ToArray()));
    }

    await userManager.AddToRoleAsync(user, "User");
    return Results.Created($"/api/auth/users/{user.Id}", new { user.Id, user.Email, Role = "User" });
}).RequireRateLimiting("auth");

app.MapGet("/api/health", () => Results.Ok(new { status = "ok" }));

var api = app.MapGroup("/api").RequireAuthorization();

api.MapGet("/statuses", async (AppDbContext db, CancellationToken cancellationToken) =>
    Results.Ok(await db.Statuses.AsNoTracking().OrderBy(status => status.Id).ToListAsync(cancellationToken)));

api.MapGet("/locations", async (AppDbContext db, CancellationToken cancellationToken) =>
    Results.Ok(await db.Locations.AsNoTracking().OrderBy(location => location.Id).ToListAsync(cancellationToken)));

api.MapGet("/inventory", async (AppDbContext db, CancellationToken cancellationToken) =>
    Results.Ok(await db.InventoryItems
        .AsNoTracking()
        .Include(item => item.CurrentStatus)
        .Include(item => item.CurrentLocation)
        .OrderBy(item => item.ManifestNumber)
        .ToListAsync(cancellationToken)));

api.MapGet("/inventory/{id:int}", async (int id, AppDbContext db, CancellationToken cancellationToken) =>
{
    var item = await db.InventoryItems
        .AsNoTracking()
        .Include(inventoryItem => inventoryItem.CurrentStatus)
        .Include(inventoryItem => inventoryItem.CurrentLocation)
        .SingleOrDefaultAsync(inventoryItem => inventoryItem.Id == id, cancellationToken);

    return item is null ? Results.NotFound() : Results.Ok(item);
});

api.MapPost("/inventory", async (InventoryItem item, AppDbContext db, CancellationToken cancellationToken) =>
{
    if (string.IsNullOrWhiteSpace(item.ManifestNumber) || string.IsNullOrWhiteSpace(item.Description))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(item.ManifestNumber)] = ["A manifest number is required."],
            [nameof(item.Description)] = ["A description is required."]
        });
    }

    if (item.QuantityExpected < 0 || item.QuantityReceived < 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(item.QuantityExpected)] = ["Quantity cannot be negative."],
            [nameof(item.QuantityReceived)] = ["Quantity cannot be negative."]
        });
    }

    item.Id = 0;
    item.DateUpdated = DateTime.UtcNow;
    db.InventoryItems.Add(item);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/inventory/{item.Id}", item);
})
    .RequireAuthorization("AdminOnly");

api.MapPut("/inventory/{id:int}", async (int id, InventoryItem input, AppDbContext db, CancellationToken cancellationToken) =>
{
    var item = await db.InventoryItems.SingleOrDefaultAsync(existing => existing.Id == id, cancellationToken);
    if (item is null) return Results.NotFound();
    if (string.IsNullOrWhiteSpace(input.ManifestNumber) || string.IsNullOrWhiteSpace(input.Description)
        || input.QuantityExpected < 0 || input.QuantityReceived < 0)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            ["item"] = ["Manifest, description, and non-negative quantities are required."]
        });
    }

    item.ManifestNumber = input.ManifestNumber.Trim();
    item.Description = input.Description.Trim();
    item.QuantityExpected = input.QuantityExpected;
    item.QuantityReceived = input.QuantityReceived;
    item.CurrentStatusId = input.CurrentStatusId;
    item.CurrentLocationId = input.CurrentLocationId;
    item.NotifyTo = input.NotifyTo?.Trim();
    item.DateUpdated = DateTime.UtcNow;
    await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
})
    .RequireAuthorization("AdminOnly");

api.MapDelete("/inventory/{id:int}", async (int id, AppDbContext db, CancellationToken cancellationToken) =>
{
    var item = await db.InventoryItems.FindAsync([id], cancellationToken);
    if (item is null) return Results.NotFound();
    db.InventoryItems.Remove(item);
    await db.SaveChangesAsync(cancellationToken);
    return Results.NoContent();
})
    .RequireAuthorization("AdminOnly");

app.Run();

static string GetDatabaseConnectionString(string fileName)
{
    var databaseDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SiteSyncTracker");
    Directory.CreateDirectory(databaseDirectory);
    return $"Data Source={Path.Combine(databaseDirectory, fileName)}";
}

public partial class Program;
