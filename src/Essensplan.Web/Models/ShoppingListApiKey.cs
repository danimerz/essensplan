namespace Essensplan.Web.Models;

/// <summary>
/// Schlüssel, mit dem eine andere App (Vorrat-App) die Einkaufsliste eines Haushalts lesen und ändern darf.
/// Gespeichert wird nur der SHA-256-Hash; der Schlüssel wird beim Erzeugen einmal angezeigt.
/// </summary>
public class ShoppingListApiKey
{
    public int Id { get; set; }
    public int HouseholdId { get; set; }
    public Household Household { get; set; } = null!;
    public string Name { get; set; } = "";
    public string KeyHash { get; set; } = "";
    /// <summary>Erste Zeichen zur Wiedererkennung („ep_AbCd…“).</summary>
    public string KeyPrefix { get; set; } = "";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
}
