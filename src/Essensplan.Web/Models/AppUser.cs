using Microsoft.AspNetCore.Identity;

namespace Essensplan.Web.Models;

public class AppUser : IdentityUser
{
    public string DisplayName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;
    public List<HouseholdMembership> HouseholdMemberships { get; set; } = new();
    public List<RecipeRating> Ratings { get; set; } = new();
}
