namespace Essensplan.Web.Models;

public class Household
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<HouseholdMembership> Memberships { get; set; } = new();
    public List<Recipe> Recipes { get; set; } = new();
    public List<Menu> Menus { get; set; } = new();
    public List<WeekPlan> WeekPlans { get; set; } = new();
}
