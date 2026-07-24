using System.Net.Http.Json;
using System.Text.Json.Serialization;

namespace Essensplan.Web.Services;

public class MigrosImageService(HttpClient http)
{
    public async Task<string?> GetImageAsync(string ingredientName)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(4));
            var url = $"/image?q={Uri.EscapeDataString(ingredientName)}";
            var result = await http.GetFromJsonAsync<MigrosImageResponse>(url, cts.Token);
            return string.IsNullOrWhiteSpace(result?.ImageUrl) ? null : result.ImageUrl;
        }
        catch
        {
            return null;
        }
    }

    private record MigrosImageResponse(
        [property: JsonPropertyName("imageUrl")] string? ImageUrl);
}
