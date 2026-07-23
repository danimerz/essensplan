using System.ComponentModel.DataAnnotations;

namespace Essensplan.Web.Models;

/// <summary>Join entity linking a Menu to one of its Recipes, with an optional role label (e.g. "Hauptgericht", "Beilage").</summary>
public class MenuRecipe
{
    public int Id { get; set; }

    public int MenuId { get; set; }
    public Menu? Menu { get; set; }

    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }

    [StringLength(50)]
    public string? Role { get; set; }

    public int SortOrder { get; set; }
}
