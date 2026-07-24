using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Essensplan.Web.Services;

public class OpenFoodFactsService(HttpClient http)
{
    private static readonly SemaphoreSlim _gate = new(4, 4);

    public async Task<string?> GetImageAsync(string ingredientName, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            using var reqCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            reqCts.CancelAfter(TimeSpan.FromSeconds(6));

            // No sort_by → Elasticsearch relevance ranking (better than most-scanned-globally)
            var url = "https://world.openfoodfacts.org/api/v2/search" +
                      $"?q={Uri.EscapeDataString(ingredientName)}" +
                      "&fields=product_name,image_front_small_url&page_size=10";

            var result = await http.GetFromJsonAsync<OffResponse>(url, reqCts.Token);
            var products = result?.Products ?? [];

            // Prefer a product whose name actually contains the ingredient
            var nameMatch = products.FirstOrDefault(p =>
                !string.IsNullOrWhiteSpace(p.ImageUrl) &&
                p.ProductName?.Contains(ingredientName, StringComparison.OrdinalIgnoreCase) == true);

            return nameMatch?.ImageUrl
                ?? products.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.ImageUrl))?.ImageUrl;
        }
        catch
        {
            return null;
        }
        finally
        {
            _gate.Release();
        }
    }

    private record OffResponse([property: JsonPropertyName("products")] List<OffProduct>? Products);

    private record OffProduct(
        [property: JsonPropertyName("product_name")] string? ProductName,
        [property: JsonPropertyName("image_front_small_url")] string? ImageUrl);
}
