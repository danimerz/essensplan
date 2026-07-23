namespace Essensplan.Web.Models;

/// <summary>A single planned week, identified by the Monday it starts on.</summary>
public class WeekPlan
{
    public int Id { get; set; }

    /// <summary>Monday of the planned week (date only).</summary>
    public DateOnly StartDate { get; set; }

    public int HouseholdId { get; set; }
    public Household? Household { get; set; }

    public List<WeekPlanEntry> Entries { get; set; } = new();
}
