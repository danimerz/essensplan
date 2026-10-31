using System.Security.Cryptography;
using System.Text;
using Essensplan.Web.Data;
using Essensplan.Web.Models;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace Essensplan.Web.Services;

/// <summary>Verwaltung der Schlüssel für die Einkaufslisten-API (nur Haushalts-Admins).</summary>
public class ShoppingListApiKeyService(IDbContextFactory<AppDbContext> dbFactory)
{
    public static string Hash(string key) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(key))).ToLowerInvariant();

    public async Task<string> CreateAsync(int householdId, string name)
    {
        var key = "ep_" + WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        await using var db = await dbFactory.CreateDbContextAsync();
        db.ShoppingListApiKeys.Add(new ShoppingListApiKey
        {
            HouseholdId = householdId,
            Name = string.IsNullOrWhiteSpace(name) ? "Vorrat-App" : name.Trim()[..Math.Min(name.Trim().Length, 100)],
            KeyHash = Hash(key),
            KeyPrefix = key[..9],
        });
        await db.SaveChangesAsync();
        return key;
    }

    public async Task<List<ShoppingListApiKey>> ListAsync(int householdId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ShoppingListApiKeys.AsNoTracking().Where(k => k.HouseholdId == householdId)
            .OrderByDescending(k => k.CreatedAt).ToListAsync();
    }

    public async Task RevokeAsync(int householdId, int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        await db.ShoppingListApiKeys.Where(k => k.Id == id && k.HouseholdId == householdId).ExecuteDeleteAsync();
    }
}

/// <summary>
/// REST-Schnittstelle der Einkaufsliste für die Vorrat-App: <c>Authorization: Bearer ep_…</c>.
/// Jede Anfrage ist auf den Haushalt des Schlüssels beschränkt.
/// </summary>
public static class ShoppingListApi
{
    public record ItemDto(int Id, string Name, decimal? Quantity, string? Unit, bool IsDone, bool IsManual,
        string? ImageUrl, string? Category, string? Price, bool IsPromotion);
    public record AddRequest(string Name);

    public static void Map(IEndpointRouteBuilder app)
    {
        var g = app.MapGroup("/api/einkaufsliste").DisableAntiforgery();

        g.MapGet("", async (HttpContext http, IDbContextFactory<AppDbContext> dbf) =>
        {
            var hid = await ResolveAsync(http, dbf);
            if (hid is null) return Results.Unauthorized();
            await using var db = await dbf.CreateDbContextAsync();
            var items = await db.ShoppingListItems.AsNoTracking()
                .Where(i => i.HouseholdId == hid)
                .OrderBy(i => i.IsDone).ThenBy(i => i.IsManual).ThenBy(i => i.SortOrder).ThenBy(i => i.Name)
                .Select(i => new ItemDto(i.Id, i.Name, i.Quantity, i.Unit, i.IsDone, i.IsManual, i.ImageUrl, i.ProductCategory, i.Price, i.IsPromotion))
                .ToListAsync();
            var name = await db.Households.Where(h => h.Id == hid).Select(h => h.Name).FirstOrDefaultAsync();
            return Results.Ok(new { household = name, items });
        });

        // Hinzufügen; ist der Artikel schon offen auf der Liste, wird nichts doppelt angelegt
        g.MapPost("", async (HttpContext http, IDbContextFactory<AppDbContext> dbf, ShoppingListService list, AddRequest req) =>
        {
            var hid = await ResolveAsync(http, dbf);
            if (hid is null) return Results.Unauthorized();
            var name = req.Name?.Trim() ?? "";
            if (name.Length is 0 or > 200) return Results.BadRequest(new { error = "Name fehlt oder ist zu lang." });
            await using (var db = await dbf.CreateDbContextAsync())
            {
                var exists = await db.ShoppingListItems.AnyAsync(i => i.HouseholdId == hid && !i.IsDone && i.Name == name);
                if (exists) return Results.Ok(new { added = false });
            }
            await list.AddManualItemAsync(hid.Value, name);
            return Results.Ok(new { added = true });
        });

        g.MapPost("/{id:int}/toggle", async (HttpContext http, IDbContextFactory<AppDbContext> dbf, int id) =>
        {
            var hid = await ResolveAsync(http, dbf);
            if (hid is null) return Results.Unauthorized();
            await using var db = await dbf.CreateDbContextAsync();
            var item = await db.ShoppingListItems.FirstOrDefaultAsync(i => i.Id == id && i.HouseholdId == hid);
            if (item is null) return Results.NotFound();
            item.IsDone = !item.IsDone;
            await db.SaveChangesAsync();
            return Results.Ok(new { item.IsDone });
        });

        // Offenen Eintrag mit diesem Namen abhaken (z.B. nach dem Einlagern in der Vorrat-App)
        g.MapPost("/abhaken", async (HttpContext http, IDbContextFactory<AppDbContext> dbf, AddRequest req) =>
        {
            var hid = await ResolveAsync(http, dbf);
            if (hid is null) return Results.Unauthorized();
            var name = req.Name?.Trim() ?? "";
            await using var db = await dbf.CreateDbContextAsync();
            var item = await db.ShoppingListItems.FirstOrDefaultAsync(i => i.HouseholdId == hid && !i.IsDone && i.Name == name);
            if (item is null) return Results.Ok(new { done = false });
            item.IsDone = true;
            await db.SaveChangesAsync();
            return Results.Ok(new { done = true, item.Id });
        });

        g.MapDelete("/{id:int}", async (HttpContext http, IDbContextFactory<AppDbContext> dbf, int id) =>
        {
            var hid = await ResolveAsync(http, dbf);
            if (hid is null) return Results.Unauthorized();
            await using var db = await dbf.CreateDbContextAsync();
            var n = await db.ShoppingListItems.Where(i => i.Id == id && i.HouseholdId == hid).ExecuteDeleteAsync();
            return n > 0 ? Results.NoContent() : Results.NotFound();
        });

        g.MapPost("/erledigte-entfernen", async (HttpContext http, IDbContextFactory<AppDbContext> dbf) =>
        {
            var hid = await ResolveAsync(http, dbf);
            if (hid is null) return Results.Unauthorized();
            await using var db = await dbf.CreateDbContextAsync();
            var n = await db.ShoppingListItems.Where(i => i.HouseholdId == hid && i.IsDone).ExecuteDeleteAsync();
            return Results.Ok(new { removed = n });
        });
    }

    private static async Task<int?> ResolveAsync(HttpContext http, IDbContextFactory<AppDbContext> dbf)
    {
        var auth = http.Request.Headers.Authorization.ToString();
        if (!auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return null;
        var key = auth["Bearer ".Length..].Trim();
        if (!key.StartsWith("ep_", StringComparison.Ordinal) || key.Length > 100) return null;
        var hash = ShoppingListApiKeyService.Hash(key);
        await using var db = await dbf.CreateDbContextAsync();
        var row = await db.ShoppingListApiKeys.FirstOrDefaultAsync(k => k.KeyHash == hash);
        if (row is null) return null;
        // „zuletzt benutzt“ höchstens alle 10 Minuten schreiben
        if (row.LastUsedAt is null || row.LastUsedAt < DateTime.UtcNow.AddMinutes(-10))
        {
            row.LastUsedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
        return row.HouseholdId;
    }
}
