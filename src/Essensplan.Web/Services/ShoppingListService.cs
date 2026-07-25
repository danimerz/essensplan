using System.Text.RegularExpressions;
using Essensplan.Web.Data;
using Essensplan.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace Essensplan.Web.Services;

public class ShoppingListService(IDbContextFactory<AppDbContext> dbFactory, MigrosImageService migrosImages)
{
    // Word-based: if any single word in the ingredient name matches, it's equipment/non-food
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
        "frischhaltefolie", "klarsichtfolie", "alufolie", "backpapier", "backpapiers",
        "zahnstocher", "spieße", "holzspieße", "spaghettimaschine",
    };

    // Matches "½ TL Salz", "1½ EL Öl", "200 g Mehl" — quantity+unit embedded in name field
    private static readonly Regex EmbeddedQtyRegex = new(
        @"^[½¼¾⅓⅔⅛⅜⅝⅞\d\s/.,]+\s*(TL|EL|g|kg|ml|dl|l|Tassen?|Prisen?|Msp\.?|Pkg\.?|Stk\.?|Stücke?|Scheiben?|Bund|Dosen?|Glas|Becher|cm)\s+(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ParensRegex = new(@"\s*\([^)]*\)\s*", RegexOptions.Compiled);

    // Ordered category rules — first match wins
    // Checks if the ingredient name (lowercase) contains any of the listed substrings
    private static readonly (string[] Terms, string Category)[] CategoryRules =
    [
        (["butter", "bratbutter", "milch", "halbmilch", "vollmilch", "rahm", "halbrahm", "obers",
          "sahne", "sauerrahm", "schmand", "kaffeesahne", "buttermilch", "kondensmilch",
          "joghurt", "yoghurt", "skyr",
          "käse", "frischkäse", "mascarpone", "ricotta", "mozzarella", "parmesan",
          "gruyère", "gruyere", "emmentaler", "feta", "gorgonzola", "brie", "camembert",
          "appenzeller", "raclette", "hüttenkäse", "quark",
          "crème fraîche", "creme fraiche", "créme fraîche",
          "eier", " ei ", " ei,"],
         "Milch & Milchprodukte"),

        (["rindfleisch", "schweinefleisch", "kalbfleisch", "lammfleisch", "hackfleisch", "gehacktes",
          "hühnchen", "hähnchen", "poulet", "geflügel", "ente", "gans", "truthahn", "pute",
          "speck", "schinken", "prosciutto", "pancetta", "guanciale", "bacon",
          "bratwurst", "cervelat", "lyoner", "mortadella", "salami", "chorizo", "landjäger", "mettwurst",
          "lachs", "forelle", "zander", "kabeljau", "thunfisch", "hecht", "tilapia", "makrele", "hering",
          "garnelen", "crevetten", "muscheln", "tintenfisch", "calamari", "anchovi", "sardinen",
          "fisch", "meeresfrüchte"],
         "Fleisch & Fisch"),

        (["spaghetti", "penne", "fusilli", "tagliatelle", "linguine", "fettuccine", "lasagne",
          "rigatoni", "farfalle", "tortellini", "ravioli", "hörnchen", "conchiglie", "orecchiette",
          "gnocchi", "couscous", "bulgur", "quinoa", "polenta", "griess",
          "basmati", "jasmin", "risotto", "arborio", "carnaroli",
          "haferflocken", "granola", "müsli", "cornflakes",
          "linsen", "kichererbsen", "kidneybohnen", "canellini", "weisse bohnen", "schwarze bohnen",
          "mehl", "weissmehl", "vollkornmehl", "dinkelmehl", "roggenmehl", "hartweizengriess",
          "nudeln", "pasta", "teigwaren", "reis "],
         "Pasta, Reis & Körner"),

        (["brot", "toast", "vollkornbrot", "weissbrot", "ruchbrot", "dinkelbrot",
          "brötchen", "semmel", "weggli", "baguette", "ciabatta", "focaccia",
          "croissant", "zopf", "hefezopf", "laugenstange"],
         "Brot & Backwaren"),

        (["zucker", "puderzucker", "vanillezucker", "rohrzucker", "brauner zucker", "muscovado",
          "honig", "ahornsirup", "agavendicksaft", "reissirup",
          "schokolade", "kakaopulver", "kakao", "schokotropfen", "kuvertüre", "nuss-nougat", "nutella",
          "marzipan", "mandeln", "walnüsse", "haselnüsse", "cashews", "pistazien", "pekannüsse", "erdnüsse",
          "backpulver", "natron", "weinstein",
          "hefe", "trockenhefe", "frischhefe",
          "vanille", "vanillemark", "vanilleextrakt", "vanilleschote",
          "stärke", "maizena", "speisestärke", "kartoffelstärke",
          "gelatine", "agar", "pektin",
          "rosinen", "sultaninen", "cranberries", "datteln", "feigen", "aprikosen", "pflaumen",
          "mohn", "sesam", "leinsamen", "chiasamen", "kürbiskerne", "sonnenblumenkerne"],
         "Süsses & Backzutaten"),

        (["olivenöl", "rapsöl", "sonnenblumenöl", "kokosöl", "sesamöl", "erdnussöl", "trüffelöl", " öl",
          "essig", "balsamico", "weinessig", "apfelessig", "reisessig",
          "senf", "mayonnaise", "ketchup", "worcester", "tabasco", "sriracha", "sambal", "harissa",
          "sojasauce", "tamari", "fischsauce", "austersauce", "hoisin",
          "tomatenmark", "tomatensauce", "passierte tomaten", "pelati", "sugo",
          "salz", "pfeffer", "paprikapulver", "kurkuma", "kreuzkümmel", "zimt", "muskat", "nelken",
          "chili", "chilipulver", "curry", "safran", "lorbeer", "kardomom", "sternanis",
          "thymian", "rosmarin", "basilikum", "oregano", "petersilie", "schnittlauch", "dill",
          "minze", "salbei", "majoran", "estragon", "kerbel", "liebstöckel",
          "bouillon", "brühe", "fond", "fleischbrühe", "gemüsebrühe", "hühnerbrühe",
          "knoblauch", "ingwer", "kumin"],
         "Gewürze & Öle"),

        (["mineralwasser", "sprudelwasser", "hahnenwasser",
          "orangensaft", "apfelsaft", "traubensaft", "multivitaminsaft",
          "bier", "weisswein", "rotwein", "roséwein", "champagner", "prosecco", "sekt",
          "kaffee", "espresso", "cappuccino",
          "kokosmilch", "kokoswasser", "mandelmilch", "hafermilch", "sojamilch", "reismilch"],
         "Getränke"),

        (["erdbeeren", "blaubeeren", "himbeeren", "brombeeren", "kirschen", "trauben",
          "mango", "ananas", "melone", "wassermelone", "avocado",
          "bananen", "banane", "äpfel", "apfel", "birnen", "birne",
          "orangen", "orange", "zitronen", "zitrone", "limetten", "limette", "grapefruit",
          "tomaten", "tomate", "cherrytomaten", "rispentomaten",
          "zwiebel", "schalotte", "frühlingszwiebeln", "lauch", "porree",
          "karotten", "karotte", "rüebli", "möhren", "pastinaken", "sellerie",
          "zucchini", "aubergine", "paprika", "peperoni",
          "brokkoli", "blumenkohl", "rosenkohl", "kohlrabi", "kohl", "rotkohl", "spitzkohl",
          "spinat", "mangold", "rucola", "salat", "feldsalat", "endivie",
          "gurke", "rettich", "radieschen", "fenchel", "artischocke",
          "pilze", "champignons", "steinpilze", "pfifferlinge", "shiitake",
          "kartoffeln", "kartoffel", "süsskartoffeln", "yam"],
         "Gemüse & Früchte"),
    ];

    private static (decimal amount, string unit) ToBaseUnit(decimal amount, string unit) =>
        unit.ToLowerInvariant() switch
        {
            "kg" => (amount * 1000m, "g"),
            "l"  => (amount * 1000m, "ml"),
            "dl" => (amount * 100m, "ml"),
            _    => (amount, unit.ToLowerInvariant())
        };

    private static string FormatNum(decimal d)
    {
        d = Math.Round(d, 2);
        if (d == Math.Floor(d)) return ((int)d).ToString();
        return d.ToString("G4", System.Globalization.CultureInfo.InvariantCulture)
                .TrimEnd('0').TrimEnd('.');
    }

    private static string FormatGroup(decimal amount, string unit)
    {
        if (unit == "g"  && amount >= 1000m) return $"{FormatNum(amount / 1000m)} kg";
        if (unit == "ml" && amount >= 1000m) return $"{FormatNum(amount / 1000m)} l";
        if (unit == "ml" && amount >= 100m)  return $"{FormatNum(amount / 100m)} dl";
        return $"{FormatNum(amount)} {unit}";
    }

    private static string? AggregateQuantities(IEnumerable<(decimal? Amount, string? Unit)> sources)
    {
        var items = sources
            .Where(x => x.Amount.HasValue && !string.IsNullOrWhiteSpace(x.Unit))
            .Select(x => ToBaseUnit(x.Amount!.Value, x.Unit!))
            .ToList();
        if (items.Count == 0) return null;

        var groups = items
            .GroupBy(x => x.unit)
            .ToDictionary(g => g.Key, g => g.Sum(x => x.amount));

        return string.Join(" + ", groups.Select(kv => FormatGroup(kv.Value, kv.Key)));
    }

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

    private static string InferCategory(string name)
    {
        // Pad with spaces so we can do whole-word matching on short terms like " öl", " ei "
        var padded = " " + name.ToLowerInvariant() + " ";
        foreach (var (terms, category) in CategoryRules)
            if (terms.Any(t => padded.Contains(t, StringComparison.OrdinalIgnoreCase)))
                return category;
        return "Sonstiges";
    }

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

        // Normalize names, filter equipment, aggregate quantities
        var newItems = ingredients
            .Select(i => (Name: NormalizeName(i.Name), i.Quantity, i.Unit))
            .Where(x => !string.IsNullOrWhiteSpace(x.Name) && !IsEquipment(x.Name))
            .GroupBy(x => x.Name.ToLowerInvariant())
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
            .Select((g, idx) =>
            {
                var name = g.First().Name;
                var displayQty = AggregateQuantities(g.Select(x => (x.Quantity, x.Unit)));
                return new ShoppingListItem
                {
                    HouseholdId = householdId,
                    Name = name,
                    Unit = displayQty,
                    ProductCategory = InferCategory(name),
                    IsDone = false,
                    IsManual = false,
                    SortOrder = idx
                };
            })
            .ToList();

        // Enrich with Migros product info: image, category (overrides keyword inference), price, promotion
        await Task.WhenAll(newItems.Select(async item =>
        {
            var info = await migrosImages.GetProductInfoAsync(item.Name);
            if (info is null) return;
            if (!string.IsNullOrWhiteSpace(info.ImageUrl))
                item.ImageUrl = info.ImageUrl;
            if (!string.IsNullOrWhiteSpace(info.Category))
                item.ProductCategory = info.Category;
            item.Price = info.Price;
            item.IsPromotion = info.IsPromotion;
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
        var trimmed = name.Trim();
        var info = await migrosImages.GetProductInfoAsync(trimmed);
        await using var db = await dbFactory.CreateDbContextAsync();
        db.ShoppingListItems.Add(new ShoppingListItem
        {
            HouseholdId = householdId,
            Name = trimmed,
            ProductCategory = info?.Category ?? InferCategory(trimmed),
            ImageUrl = string.IsNullOrWhiteSpace(info?.ImageUrl) ? null : info.ImageUrl,
            Price = info?.Price,
            IsPromotion = info?.IsPromotion ?? false,
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
