using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Essensplan.Web.Services;

public class OpenFoodFactsService(HttpClient http)
{
    private static readonly SemaphoreSlim _gate = new(5, 5);

    private static readonly (string[] Keywords, string Category)[] CategoryRules =
    [
        (["dairi", "milk", "butter", "cheese", "yogurt", "cream", "lait", "fromage", "ei", "egg"],
            "Milch & Milchprodukte"),
        (["meat", "beef", "pork", "chicken", "poultry", "fish", "seafood", "sausage", "fleisch", "wurst", "fisch"],
            "Fleisch & Fisch"),
        (["bread", "bakery", "biscuit", "cracker", "pastri", "brot", "gebäck", "toast"],
            "Brot & Backwaren"),
        (["vegetable", "fruit", "fresh-food", "produce", "gemüse", "obst", "früchte", "salat"],
            "Gemüse & Früchte"),
        (["pasta", "noodle", "rice", "cereal", "grain", "flour", "legume", "pulse", "getreide", "nudel", "mehl", "reis", "hülsen"],
            "Pasta, Reis & Körner"),
        (["spice", "herb", "oil", "vinegar", "condiment", "sauce", "seasoning", "gewürz", "öl", "essig", "senf"],
            "Gewürze & Öle"),
        (["beverage", "drink", "juice", "water", "soda", "coffee", "tea", "getränk", "wasser", "saft", "kaffee"],
            "Getränke"),
        (["sweet", "chocolate", "candy", "sugar", "dessert", "biscuit", "confection", "süss", "zucker", "schokolade", "backzutat"],
            "Süsses & Backzutaten"),
        (["frozen", "tiefkühl"],
            "Tiefkühlprodukte"),
    ];

    public async Task<(string? ImageUrl, string Category)> LookupAsync(string name, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            using var reqCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            reqCts.CancelAfter(TimeSpan.FromSeconds(5));

            var url = "https://world.openfoodfacts.org/cgi/search.pl" +
                      $"?search_terms={Uri.EscapeDataString(name)}" +
                      "&action=process&json=1&fields=image_small_url,categories_tags&page_size=5&sort_by=unique_scans_n";

            var result = await http.GetFromJsonAsync<OffResponse>(url, reqCts.Token);
            var product = result?.Products?.FirstOrDefault(
                p => p.ImageSmallUrl is not null || p.CategoriesTags is { Count: > 0 });

            if (product is null)
                return (null, "Sonstiges");

            var category = MapCategory(product.CategoriesTags ?? []);
            return (product.ImageSmallUrl, category);
        }
        catch
        {
            return (null, "Sonstiges");
        }
        finally
        {
            _gate.Release();
        }
    }

    private static string MapCategory(List<string> tags)
    {
        foreach (var (keywords, category) in CategoryRules)
            if (tags.Any(t => keywords.Any(k => t.Contains(k, StringComparison.OrdinalIgnoreCase))))
                return category;
        return "Sonstiges";
    }

    private record OffResponse([property: JsonPropertyName("products")] List<OffProduct>? Products);

    private record OffProduct(
        [property: JsonPropertyName("image_small_url")] string? ImageSmallUrl,
        [property: JsonPropertyName("categories_tags")] List<string>? CategoriesTags);
}
