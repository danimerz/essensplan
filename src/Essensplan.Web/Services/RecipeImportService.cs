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
/// </summary>
public class RecipeImportService
{
    private static readonly Regex JsonLdScriptRegex = new(
        "<script[^>]+type=[\"']application/ld\\+json[\"'][^>]*>(.*?)</script>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex IsoDurationRegex = new(
        @"P(?:(?<days>\d+)D)?T?(?:(?<hours>\d+)H)?(?:(?<minutes>\d+)M)?",
        RegexOptions.Compiled);

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
        foreach (Match match in JsonLdScriptRegex.Matches(html))
        {
            var json = System.Net.WebUtility.HtmlDecode(match.Groups[1].Value).Trim();
            if (string.IsNullOrWhiteSpace(json)) continue;

            JsonDocument doc;
            try
            {
                doc = JsonDocument.Parse(json);
            }
            catch (JsonException)
            {
                continue;
            }

            using (doc)
            {
                var recipeElement = FindRecipeNode(doc.RootElement);
                if (recipeElement.HasValue)
                {
                    var recipe = MapRecipe(recipeElement.Value);
                    if (recipe is not null) return recipe;
                }
            }
        }

        return null;
    }

    private static JsonElement? FindRecipeNode(JsonElement root)
    {
        switch (root.ValueKind)
        {
            case JsonValueKind.Object:
                if (IsRecipeType(root)) return root;

                if (root.TryGetProperty("@graph", out var graph) && graph.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in graph.EnumerateArray())
                    {
                        var found = FindRecipeNode(item);
                        if (found.HasValue) return found;
                    }
                }
                return null;

            case JsonValueKind.Array:
                foreach (var item in root.EnumerateArray())
                {
                    var found = FindRecipeNode(item);
                    if (found.HasValue) return found;
                }
                return null;

            default:
                return null;
        }
    }

    private static bool IsRecipeType(JsonElement obj)
    {
        if (!obj.TryGetProperty("@type", out var typeProp)) return false;

        if (typeProp.ValueKind == JsonValueKind.String)
        {
            return string.Equals(typeProp.GetString(), "Recipe", StringComparison.OrdinalIgnoreCase);
        }

        if (typeProp.ValueKind == JsonValueKind.Array)
        {
            return typeProp.EnumerateArray()
                .Any(t => t.ValueKind == JsonValueKind.String &&
                          string.Equals(t.GetString(), "Recipe", StringComparison.OrdinalIgnoreCase));
        }

        return false;
    }

    private static Recipe? MapRecipe(JsonElement node)
    {
        var name = GetString(node, "name");
        if (string.IsNullOrWhiteSpace(name)) return null;

        var recipe = new Recipe
        {
            Name = name.Trim(),
            Description = GetString(node, "description")?.Trim(),
            ImageUrl = GetImageUrl(node),
            PrepTimeMinutes = ParseIsoDurationMinutes(GetString(node, "prepTime")),
            CookTimeMinutes = ParseIsoDurationMinutes(GetString(node, "cookTime")),
            Servings = ParseServings(node),
            Instructions = GetInstructions(node),
            Ingredients = GetIngredients(node)
        };

        return recipe;
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
        if (!node.TryGetProperty("recipeYield", out var yieldEl)) return 4;

        string? text = yieldEl.ValueKind switch
        {
            JsonValueKind.String => yieldEl.GetString(),
            JsonValueKind.Number => yieldEl.GetRawText(),
            JsonValueKind.Array when yieldEl.GetArrayLength() > 0 => yieldEl[0].GetString(),
            _ => null
        };

        if (string.IsNullOrWhiteSpace(text)) return 4;

        var digits = Regex.Match(text, @"\d+");
        return digits.Success && int.TryParse(digits.Value, out var n) && n > 0 ? n : 4;
    }

    private static string? GetInstructions(JsonElement node)
    {
        if (!node.TryGetProperty("recipeInstructions", out var instr)) return null;

        var steps = new List<string>();
        CollectInstructionSteps(instr, steps);

        if (steps.Count == 0) return null;

        return string.Join("\n", steps.Select((s, i) => $"{i + 1}. {s}"));
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
                {
                    CollectInstructionSteps(item, steps);
                }
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

        var propName = node.TryGetProperty("recipeIngredient", out var ing) ? "recipeIngredient"
                      : node.TryGetProperty("ingredients", out ing) ? "ingredients"
                      : null;

        if (propName is null || ing.ValueKind != JsonValueKind.Array) return result;

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

    private static readonly Regex IngredientLineRegex = new(
        @"^(?<qty>\d+(?:[.,]\d+)?(?:\s*[-–]\s*\d+(?:[.,]\d+)?)?)\s*(?<unit>[a-zA-ZäöüÄÖÜß.]{1,15})?\s+(?<name>.+)$",
        RegexOptions.Compiled);

    private static (decimal? quantity, string? unit, string name) ParseIngredientLine(string raw)
    {
        var match = IngredientLineRegex.Match(raw);
        if (!match.Success)
        {
            return (null, null, raw);
        }

        decimal? qty = null;
        var qtyText = match.Groups["qty"].Value.Replace(",", ".");
        if (qtyText.Contains('-') || qtyText.Contains('–'))
        {
            // Range like "2-3" -> take the first number.
            var first = Regex.Match(qtyText, @"[\d.]+");
            if (first.Success && decimal.TryParse(first.Value, System.Globalization.CultureInfo.InvariantCulture, out var q))
            {
                qty = q;
            }
        }
        else if (decimal.TryParse(qtyText, System.Globalization.CultureInfo.InvariantCulture, out var q2))
        {
            qty = q2;
        }

        var unit = match.Groups["unit"].Success ? match.Groups["unit"].Value : null;
        var name = match.Groups["name"].Value.Trim();

        return (qty, string.IsNullOrWhiteSpace(unit) ? null : unit, string.IsNullOrWhiteSpace(name) ? raw : name);
    }

    private static int ParseIsoDurationMinutes(string? iso)
    {
        if (string.IsNullOrWhiteSpace(iso)) return 0;

        var match = IsoDurationRegex.Match(iso);
        if (!match.Success) return 0;

        var days = match.Groups["days"].Success ? int.Parse(match.Groups["days"].Value) : 0;
        var hours = match.Groups["hours"].Success ? int.Parse(match.Groups["hours"].Value) : 0;
        var minutes = match.Groups["minutes"].Success ? int.Parse(match.Groups["minutes"].Value) : 0;

        return days * 24 * 60 + hours * 60 + minutes;
    }
}
