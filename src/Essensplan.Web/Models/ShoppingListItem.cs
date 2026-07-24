using System.ComponentModel.DataAnnotations.Schema;

namespace Essensplan.Web.Models;

public class ShoppingListItem
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string Name { get; set; } = "";
    [Column(TypeName = "decimal(10,2)")]
    public decimal? Quantity { get; set; }
    public string? Unit { get; set; }
    public bool IsDone { get; set; }
    public bool IsManual { get; set; }
    public int SortOrder { get; set; }
    public string? ImageUrl { get; set; }
    public string? ProductCategory { get; set; }
    public string? Price { get; set; }
    public bool IsPromotion { get; set; }
}
