using System.ComponentModel.DataAnnotations;

namespace Essensplan.Web.Models;

public class RecipeRating
{
    public int Id { get; set; }
    public int RecipeId { get; set; }
    public Recipe Recipe { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;
    public int HouseholdId { get; set; }

    [Range(1, 5)]
    public int Stars { get; set; } = 5;

    [StringLength(1000)]
    public string? Comment { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
