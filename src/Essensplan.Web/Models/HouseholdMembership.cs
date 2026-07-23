namespace Essensplan.Web.Models;

public enum HouseholdRole { Member = 0, Admin = 1 }

public class HouseholdMembership
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string UserId { get; set; } = string.Empty;
    public AppUser User { get; set; } = null!;
    public HouseholdRole Role { get; set; } = HouseholdRole.Member;
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
}
