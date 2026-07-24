using System.Text.RegularExpressions;
using Essensplan.Web.Data;
using Essensplan.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Essensplan.Web.Services;

public class ShoppingListService(IDbContextFactory<AppDbContext> dbFactory, OpenFoodFactsService offService)
{
    // Word-based: if any single word in the ingredient name matches, it's equipment
    private static readonly HashSet<string> KitchenEquipment = new(StringComparer.OrdinalIgnoreCase)
    {
        "backblech", "blech", "backform", "springform", "kuchenform", "tortenform", "muffinform", "auflaufform",
        "pfanne", "bratpfanne", "topf", "kochtopf", "kasserolle", "wok",
        "schüssel", "rührschüssel", "salatschüssel",
        "schneidebrett", "holzbrett",
        "schneebesen", "rührbesen", "kochlöffel", "pfannenwender", "schöpflöffel",
        "nudelholz", "teigrolle",
        "sieb", "abtropfsieb", "passiersieb",
        "reibe", "käsereibe", "sparschäler", "schäler",
        "handrührgerät", "küchenmaschine", "mixer", "stabmixer",
        "mörser", "stößel",
        "gitter", "kuchengitter", "abkühlgitter",
        "messbecher", "küchenwaage", "waage",
        "frischhaltefolie", "alufolie",
        "zahnstocher", "spieße", "holzspieße",
    };

    // Matches "½ TL Salz", "1½ EL Öl", "200 g Mehl" — quantity+unit embedded in name field
    private static readonly Regex EmbeddedQtyRegex = new(
        @"^[½¼¾⅓⅔⅛⅜⅝⅞\d\s/.,]+\s*(TL|EL|g|kg|ml|dl|l|Tassen?|Prisen?|Msp\.?|Pkg\.?|Stk\.?|Stücke?|Scheiben?|Bund|Dosen?|Glas|Becher|cm)\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ParensRegex = new(@"\s*\([^)]*\)\s*", RegexOptions.Compiled);

    private static string NormalizeName(string raw)
    {
        var name = raw.Trim();

        // Extract name from "½ TL Salz" or "200 g Mehl" (quantity embedded in name field)
        var m = EmbeddedQtyRegex.Match(name);
        if (m.Success)
            name = m.Groups[2].Value.Trim();

        // Strip parenthetical notes: "Mehl (Halbweissmehl oder ...)" → "Mehl"
        name = ParensRegex.Replace(name, " ").Trim();

        // Strip comma-qualifier: "Butter, flüssig" → "Butter"
        var comma = name.IndexOf(',');
        if (comma > 1)
            name = name[..comma].Trim();

        return name;
    }

    private static bool IsEquipment(string name) =>
        name.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(word => KitchenEquipment.Contains(word.TrimEnd('.', ',', ';', ':').TrimStart('(')));

    public async Task<List<ShoppingListItem>> GetItemsAsync(int householdId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        return await db.ShoppingListItems
            .Where(i => i.HouseholdId == householdId)
            .OrderBy(i => i.IsDone)
            .ThenBy(i => i.IsManual)
            .ThenBy(i => i.SortOrder)
            .ThenBy(i => i.Name)
            .ToListAsync();
    }

    public async Task GenerateFromWeekPlanAsync(int householdId, DateOnly weekStart)
    {
        await using var db = await dbFactory.CreateDbContextAsync();

        var weekPlan = await db.WeekPlans
            .Where(w => w.HouseholdId == householdId && w.StartDate == weekStart)
            .Include(w => w.Entries)
                .ThenInclude(e => e.Menu)
                    .ThenInclude(m => m!.MenuRecipes)
                        .ThenInclude(mr => mr.Recipe)
                            .ThenInclude(r => r!.Ingredients)
            .FirstOrDefaultAsync();

        var ingredients = weekPlan?.Entries
            .Where(e => e.Menu is not null)
            .SelectMany(e => e.Menu!.MenuRecipes)
            .Where(mr => mr.Recipe is not null)
            .SelectMany(mr => mr.Recipe!.Ingredients)
            .ToList() ?? [];

        // Normalize names, filter equipment, deduplicate
        var newItems = ingredients
            .Select(i => NormalizeName(i.Name))
            .Where(name => !string.IsNullOrWhiteSpace(name) && !IsEquipment(name))
            .GroupBy(name => name.ToLowerInvariant())
            .Select(g => g.First()) // keep first occurrence's casing
            .OrderBy(name => name, StringComparer.OrdinalIgnoreCase)
            .Select((name, idx) => new ShoppingListItem
            {
                HouseholdId = householdId,
                Name = name,
                IsDone = false,
                IsManual = false,
                SortOrder = idx
            })
            .ToList();

        // Enrich with Open Food Facts (parallel, max 5 concurrent via service-level semaphore)
        await Task.WhenAll(newItems.Select(async item =>
        {
            var (imageUrl, category) = await offService.LookupAsync(item.Name);
            item.ImageUrl = imageUrl;
            item.ProductCategory = category;
        }));

        // Replace existing generated items, keep manual entries
        var oldGenerated = await db.ShoppingListItems
            .Where(i => i.HouseholdId == householdId && !i.IsManual)
            .ToListAsync();
        db.ShoppingListItems.RemoveRange(oldGenerated);
        db.ShoppingListItems.AddRange(newItems);
        await db.SaveChangesAsync();
    }

    public async Task ToggleDoneAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.ShoppingListItems.FindAsync(id);
        if (item is null) return;
        item.IsDone = !item.IsDone;
        await db.SaveChangesAsync();
    }

    public async Task AddManualItemAsync(int householdId, string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        await using var db = await dbFactory.CreateDbContextAsync();
        db.ShoppingListItems.Add(new ShoppingListItem
        {
            HouseholdId = householdId,
            Name = name.Trim(),
            IsManual = true,
            SortOrder = 9999
        });
        await db.SaveChangesAsync();
    }

    public async Task DeleteItemAsync(int id)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var item = await db.ShoppingListItems.FindAsync(id);
        if (item is null) return;
        db.ShoppingListItems.Remove(item);
        await db.SaveChangesAsync();
    }

    public async Task ClearDoneAsync(int householdId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var done = await db.ShoppingListItems
            .Where(i => i.HouseholdId == householdId && i.IsDone)
            .ToListAsync();
        db.ShoppingListItems.RemoveRange(done);
        await db.SaveChangesAsync();
    }

    public async Task ClearAllAsync(int householdId)
    {
        await using var db = await dbFactory.CreateDbContextAsync();
        var all = await db.ShoppingListItems
            .Where(i => i.HouseholdId == householdId)
            .ToListAsync();
        db.ShoppingListItems.RemoveRange(all);
        await db.SaveChangesAsync();
    }

    public static string BuildCopyText(List<ShoppingListItem> items, DateOnly weekStart)
    {
        var kw = System.Globalization.ISOWeek.GetWeekOfYear(weekStart.ToDateTime(TimeOnly.MinValue));
        var lines = new System.Text.StringBuilder();
        lines.AppendLine($"🛒 Einkaufsliste – KW {kw}");
        lines.AppendLine();
        foreach (var item in items.Where(i => !i.IsDone))
            lines.AppendLine($"□ {item.Name}");
        return lines.ToString().TrimEnd();
    }
}
