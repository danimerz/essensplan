using System.ComponentModel.DataAnnotations;

namespace Essensplan.Web.Models;

public class Recipe
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    public string? Instructions { get; set; }

    public int PrepTimeMinutes { get; set; }

    public int CookTimeMinutes { get; set; }

    public int Servings { get; set; } = 4;

    [StringLength(1000)]
    public string? ImageUrl { get; set; }

    [StringLength(1000)]
    public string? SourceUrl { get; set; }

    public int? CategoryId { get; set; }
    public RecipeCategory? Category { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<RecipeIngredient> Ingredients { get; set; } = new();
    public List<MenuRecipe> MenuRecipes { get; set; } = new();
    public List<RecipeRating> Ratings { get; set; } = new();
    public List<HouseholdRecipe> HouseholdRecipes { get; set; } = new();

    public int TotalTimeMinutes => PrepTimeMinutes + CookTimeMinutes;
}
