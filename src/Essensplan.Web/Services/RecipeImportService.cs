using System.Text.Json;
using System.Text.RegularExpressions;
using Essensplan.Web.Models;

namespace Essensplan.Web.Services;

public class RecipeImportResult
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public Recipe? Recipe { get; set; }
}

/// <summary>
/// Imports a recipe from a public URL by looking for schema.org/Recipe structured data
/// (JSON-LD, embedded as &lt;script type="application/ld+json"&gt;), which the vast majority
/// of modern recipe websites publish for SEO purposes.
/// Falls back to schema.org/HowTo JSON-LD + HTML Microdata (itemprop) for sites like
/// swissmilk.ch that split their recipe data across both formats.
/// </summary>
public class RecipeImportService
{
    private static readonly Regex JsonLdScriptRegex = new(
        "<script[^>]+type=[\"']application/ld\\+json[\"'][^>]*>(.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex IsoDurationRegex = new(
        @"P(?:(?<days>\d+)D)?T?(?:(?<hours>\d+)H)?(?:(?<minutes>\d+)M)?",
        RegexOptions.Compiled);

    // Strip HTML tags and Vue/Alpine comments from a fragment
    private static readonly Regex HtmlTagRegex = new(
        @"<[^>]+>|<!--[\s\S]*?-->",
        RegexOptions.Compiled);

    private static readonly Regex WhitespaceCollapseRegex = new(
        @"\s+",
        RegexOptions.Compiled);

    // Matches <tr> or <li> elements carrying itemprop="recipeIngredient"
    // Backreference \1 ensures closing tag matches opening tag
    private static readonly Regex MicrodataIngredientRegex = new(
        @"<(tr|li)\s[^>]*itemprop=[""']recipeIngredient[""'][^>]*>([\s\S]*?)</\1>",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex MicrodataYieldRegex = new(
        @"itemprop=[""']recipeYield[""'][^>]*>\s*([^<]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly HttpClient _httpClient;

    public RecipeImportService(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<RecipeImportResult> ImportFromUrlAsync(string url, CancellationToken ct = default)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || (uri.Scheme != "http" && uri.Scheme != "https"))
        {
            return new RecipeImportResult { Success = false, ErrorMessage = "Bitte eine gültige URL (http/https) angeben." };
        }

        string html;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, uri);
            request.Headers.UserAgent.ParseAdd("Mozilla/5.0 (compatible; EssensplanBot/1.0; +https://example.local)");
            using var response = await _httpClient.SendAsync(request, ct);
            response.EnsureSuccessStatusCode();
            html = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex)
        {
            return new RecipeImportResult { Success = false, ErrorMessage = $"Seite konnte nicht geladen werden: {ex.Message}" };
        }

        var recipe = TryExtractRecipe(html);
        if (recipe is null)
        {
            return new RecipeImportResult
            {
                Success = false,
                ErrorMessage = "Auf dieser Seite wurden keine strukturierten Rezeptdaten (schema.org/Recipe) gefunden. Bitte manuell erfassen."
            };
        }

        recipe.SourceUrl = url;
        return new RecipeImportResult { Success = true, Recipe = recipe };
    }

    private static Recipe? TryExtractRecipe(string html)
    {
        var jsonLdBlocks = CollectJsonLdBlocks(html);

        // Pass 1: prefer a proper Recipe node in JSON-LD
        foreach (var doc in jsonLdBlocks)
        {
            using (doc)
            {
                var node = FindNodeByType(doc.RootElement, "Recipe");
                if (!node.HasValue) continue;
                var recipe = MapRecipe(node.Value);
                if (recipe is not null)
                {
                    ApplyServingsDefault(recipe);
                    return recipe;
                }
            }
        }

        // Pass 2: HowTo JSON-LD + supplement from HTML Microdata
        // (used by sites like swissmilk.ch that split recipe data across both formats)
        foreach (var doc in CollectJsonLdBlocks(html))
        {
            using (doc)
            {
                var node = FindNodeByType(doc.RootElement, "HowTo");
                if (!node.HasValue) continue;
                var recipe = MapRecipe(node.Value);
                if (recipe is null) continue;

                if (!recipe.Ingredients.Any())
                    recipe.Ingredients = ExtractMicrodataIngredients(html);

                if (recipe.Servings <= 0)
                    recipe.Servings = ExtractMicrodataServings(html);

                ApplyServingsDefault(recipe);
                return recipe;
            }
        }

        return null;
    }

    private static List<JsonDocument> CollectJsonLdBlocks(string html)
    {
        var docs = new List<JsonDocument>();
        foreach (Match match in JsonLdScriptRegex.Matches(html))
        {
            var json = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
            if (string.IsNullOrWhiteSpace(json)) continue;
            try { docs.Add(JsonDocument.Parse(json)); }
            catch (JsonException) { }
        }
        return docs;
    }

    private static void ApplyServingsDefault(Recipe recipe)
    {
        if (recipe.Servings <= 0) recipe.Servings = 4;
    }

    private static JsonElement? FindNodeByType(JsonElement root, string schemaType)
    {
        switch (root.ValueKind)
        {
            case JsonValueKind.Object:
                if (IsSchemaType(root, schemaType)) return root;
                if (root.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in graph.EnumerateArray())
                    {
                        var found = FindNodeByType(item, schemaType);
                        if (found.HasValue) return found;
                    }
                }
                return null;

            case JsonValueKind.Array:
                foreach (var item in root.EnumerateArray())
                {
                    var found = FindNodeByType(item, schemaType);
                    if (found.HasValue) return found;
                }
                return null;

            default:
                return null;
        }
    }

    private static bool IsSchemaType(JsonElement obj, string schemaType)
    {
        if (!obj.TryGetProperty("@type", out var typeProp)) return false;

        if (typeProp.ValueKind == JsonValueKind.String)
            return string.Equals(typeProp.GetString(), schemaType, StringComparison.OrdinalIgnoreCase);

        if (typeProp.ValueKind == JsonValueKind.Array)
            return typeProp.EnumerateArray()
                .Any(t => t.ValueKind == JsonValueKind.String &&
                          string.Equals(t.GetString(), schemaType, StringComparison.OrdinalIgnoreCase));

        return false;
    }

    private static Recipe? MapRecipe(JsonElement node)
    {
        var name = GetString(node, "name");
        if (string.IsNullOrWhiteSpace(name)) return null;

        var cookTime = ParseDurationMinutes(GetString(node, "cookTime"));
        var totalTime = ParseDurationMinutes(GetString(node, "totalTime"));

        return new Recipe
        {
            Name = name.Trim(),
            Description = GetString(node, "description")?.Trim(),
            ImageUrl = GetImageUrl(node),
            PrepTimeMinutes = ParseDurationMinutes(GetString(node, "prepTime")),
            // HowTo nodes only have totalTime; Recipe nodes usually have cookTime
            CookTimeMinutes = cookTime > 0 ? cookTime : totalTime,
            Servings = ParseServings(node),
            Instructions = GetInstructions(node),
            Ingredients = GetIngredients(node)
        };
    }

    private static string? GetString(JsonElement node, string property)
    {
        if (!node.TryGetProperty(property, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.String => value.GetString(),
            JsonValueKind.Number => value.GetRawText(),
            _ => null
        };
    }

    private static string? GetImageUrl(JsonElement node)
    {
        if (!node.TryGetProperty("image", out var image)) return null;

        return image.ValueKind switch
        {
            JsonValueKind.String => image.GetString(),
            JsonValueKind.Object when image.TryGetProperty("url", out var u) => u.GetString(),
            JsonValueKind.Array when image.GetArrayLength() > 0 => image[0].ValueKind switch
            {
                JsonValueKind.String => image[0].GetString(),
                JsonValueKind.Object when image[0].TryGetProperty("url", out var u2) => u2.GetString(),
                _ => null
            },
            _ => null
        };
    }

    private static int ParseServings(JsonElement node)
    {
        if (!node.TryGetProperty("recipeYield", out var yieldEl)) return 0;

        string? text = yieldEl.ValueKind switch
        {
            JsonValueKind.String => yieldEl.GetString(),
            JsonValueKind.Number => yieldEl.GetRawText(),
            JsonValueKind.Array when yieldEl.GetArrayLength() > 0 => yieldEl[0].GetString(),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(text)) return 0;
        var digits = Regex.Match(text, @"\d+");
        return digits.Success && int.TryParse(digits.Value, out var n) && n > 0 ? n : 0;
    }

    private static string? GetInstructions(JsonElement node)
    {
        // Recipe uses "recipeInstructions", HowTo uses "step"
        JsonElement instr;
        if (!node.TryGetProperty("recipeInstructions", out instr) &&
            !node.TryGetProperty("step", out instr))
            return null;

        var steps = new List<string>();
        CollectInstructionSteps(instr, steps);
        return steps.Count == 0 ? null : string.Join("\n", steps.Select((s, i) => $"{i + 1}. {s}"));
    }

    private static void CollectInstructionSteps(JsonElement el, List<string> steps)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.String:
                var text = el.GetString();
                if (!string.IsNullOrWhiteSpace(text)) steps.Add(text.Trim());
                break;

            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                    CollectInstructionSteps(item, steps);
                break;

            case JsonValueKind.Object:
                if (el.TryGetProperty("itemListElement", out var nested))
                {
                    CollectInstructionSteps(nested, steps);
                }
                else if (el.TryGetProperty("text", out var textProp) && textProp.ValueKind == JsonValueKind.String)
                {
                    var t = textProp.GetString();
                    if (!string.IsNullOrWhiteSpace(t)) steps.Add(t.Trim());
                }
                break;
        }
    }

    private static List<RecipeIngredient> GetIngredients(JsonElement node)
    {
        var result = new List<RecipeIngredient>();

        JsonElement ing;
        if (!node.TryGetProperty("recipeIngredient", out ing) &&
            !node.TryGetProperty("ingredients", out ing))
            return result;

        if (ing.ValueKind != JsonValueKind.Array) return result;

        var sort = 0;
        foreach (var item in ing.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            var raw = item.GetString();
            if (string.IsNullOrWhiteSpace(raw)) continue;

            var (quantity, unit, ingredientName) = ParseIngredientLine(raw.Trim());
            result.Add(new RecipeIngredient
            {
                Name = ingredientName,
                Quantity = quantity,
                Unit = unit,
                SortOrder = sort++
            });
        }

        return result;
    }

    private static List<RecipeIngredient> ExtractMicrodataIngredients(string html)
    {
        var result = new List<RecipeIngredient>();
        var sort = 0;

        foreach (Match match in MicrodataIngredientRegex.Matches(html))
        {
            // Group 2 is the inner content (group 1 is the tag name tr/li)
            var inner = match.Groups[2].Value;
            var text = WhitespaceCollapseRegex
                .Replace(HtmlTagRegex.Replace(inner, " "), " ")
                .Trim();
            if (string.IsNullOrWhiteSpace(text)) continue;

            var (quantity, unit, name) = ParseIngredientLine(text);
            result.Add(new RecipeIngredient
            {
                Name = name,
                Quantity = quantity,
                Unit = unit,
                SortOrder = sort++
            });
        }

        return result;
    }

    private static int ExtractMicrodataServings(string html)
    {
        var match = MicrodataYieldRegex.Match(html);
        if (!match.Success) return 0;
        var digits = Regex.Match(match.Groups[1].Value.Trim(), @"\d+");
        return digits.Success && int.TryParse(digits.Value, out var n) ? n : 0;
    }

    private static readonly Regex IngredientLineRegex = new(
        @"^(?<qty>\d+(?:[.,]\d+)?(?:\s*[-–]\s*\d+(?:[.,]\d+)?)?)\s*(?<unit>[a-zA-ZäöüÄÖÜß.]{1,15})?\s+(?<name>.+)$",
        RegexOptions.Compiled);

    private static (decimal? quantity, string? unit, string name) ParseIngredientLine(string raw)
    {
        var match = IngredientLineRegex.Match(raw);
        if (!match.Success) return (null, null, raw);

        decimal? qty = null;
        var qtyText = match.Groups["qty"].Value.Replace(",", ".");
        if (qtyText.Contains('-') || qtyText.Contains('–'))
        {
            var first = Regex.Match(qtyText, @"[\d.]+");
            if (first.Success && decimal.TryParse(first.Value, System.Globalization.CultureInfo.InvariantCulture, out var q))
                qty = q;
        }
        else if (decimal.TryParse(qtyText, System.Globalization.CultureInfo.InvariantCulture, out var q2))
        {
            qty = q2;
        }

        var unit = match.Groups["unit"].Success ? match.Groups["unit"].Value : null;
        var name = match.Groups["name"].Value.Trim();

        return (qty, string.IsNullOrWhiteSpace(unit) ? null : unit, string.IsNullOrWhiteSpace(name) ? raw : name);
    }

    private static int ParseDurationMinutes(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return 0;

        // Some sites (e.g. swissmilk HowTo) use a plain integer (minutes) instead of ISO 8601
        if (int.TryParse(value.Trim(), out var directMinutes)) return directMinutes;

        // ISO 8601: PT45M, P1DT30M, etc.
        var match = IsoDurationRegex.Match(value);
        if (!match.Success) return 0;

        var days = match.Groups["days"].Success ? int.Parse(match.Groups["days"].Value) : 0;
        var hours = match.Groups["hours"].Success ? int.Parse(match.Groups["hours"].Value) : 0;
        var minutes = match.Groups["minutes"].Success ? int.Parse(match.Groups["minutes"].Value) : 0;

        return days * 24 * 60 + hours * 60 + minutes;
    }
}
