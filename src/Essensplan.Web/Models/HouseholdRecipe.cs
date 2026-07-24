namespace Essensplan.Web.Models;

public class HouseholdRecipe
{
    public int HouseholdId { get; set; }
    public int RecipeId { get; set; }
    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public Household Household { get; set; } = null!;
    public Recipe Recipe { get; set; } = null!;
}
