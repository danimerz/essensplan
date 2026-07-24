namespace Essensplan.Web.Models;

public class Household
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<HouseholdMembership> Memberships { get; set; } = new();
    public List<HouseholdRecipe> HouseholdRecipes { get; set; } = new();
    public List<Menu> Menus { get; set; } = new();
    public List<WeekPlan> WeekPlans { get; set; } = new();
    public List<ShoppingListItem> ShoppingListItems { get; set; } = new();
}
