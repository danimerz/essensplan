using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Essensplan.Web.Services;

public class MigrosImageService(HttpClient http)
{
    public async Task<MigrosProductInfo?> GetProductInfoAsync(string ingredientName)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            var url = $"/image?q={Uri.EscapeDataString(ingredientName)}";
            return await http.GetFromJsonAsync<MigrosProductInfo>(url, cts.Token);
        }
        catch
        {
            return null;
        }
    }
}

public record MigrosProductInfo(
    [property: JsonPropertyName("imageUrl")] string? ImageUrl,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("price")] string? Price,
    [property: JsonPropertyName("isPromotion")] bool IsPromotion);
