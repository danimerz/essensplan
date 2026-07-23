using System.ComponentModel.DataAnnotations;

namespace Essensplan.Web.Models;

/// <summary>A menu is a plannable meal, composed of one or more recipes (e.g. main + side + dessert).</summary>
public class Menu
{
    public int Id { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    public string? Description { get; set; }

    /// <summary>Bitmask of MealTypeFlags — which meal slots this menu is eligible for.</summary>
    public int AllowedMealTypes { get; set; } = MealTypeFlags.Mittagessen | MealTypeFlags.Abendessen;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<MenuRecipe> MenuRecipes { get; set; } = new();

    public List<WeekPlanEntry> WeekPlanEntries { get; set; } = new();
}
