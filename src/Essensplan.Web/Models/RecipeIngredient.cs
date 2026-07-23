using System.ComponentModel.DataAnnotations;

namespace Essensplan.Web.Models;

public class RecipeIngredient
{
    public int Id { get; set; }

    public int RecipeId { get; set; }
    public Recipe? Recipe { get; set; }

    [Required, StringLength(150)]
    public string Name { get; set; } = string.Empty;

    public decimal? Quantity { get; set; }

    [StringLength(20)]
    public string? Unit { get; set; }

    public int SortOrder { get; set; }
}
