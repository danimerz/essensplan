using System.ComponentModel.DataAnnotations;

namespace Essensplan.Web.Models;

public class RecipeCategory
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>Emoji shown as the category icon, e.g. "🍝".</summary>
    [StringLength(10)]
    public string Icon { get; set; } = "🍽️";

    /// <summary>Hex color used for chips/badges, e.g. "#FF7A59".</summary>
    [StringLength(20)]
    public string Color { get; set; } = "#6C63FF";

    public int SortOrder { get; set; }

    public List<Recipe> Recipes { get; set; } = new();
}
