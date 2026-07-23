namespace Essensplan.Web.Models;

public enum MealType
{
    Fruehstueck = 0,
    Mittagessen = 1,
    Abendessen = 2,
    Snack = 3
}

/// <summary>
/// Bit-flags for Menu.AllowedMealTypes. Each bit corresponds to 1 &lt;&lt; (int)MealType.
/// Use this instead of the MealType enum when a menu is allowed for multiple slots.
/// </summary>
public static class MealTypeFlags
{
    public const int Fruehstueck = 1 << (int)MealType.Fruehstueck;  // 1
    public const int Mittagessen = 1 << (int)MealType.Mittagessen;  // 2
    public const int Abendessen  = 1 << (int)MealType.Abendessen;   // 4
    public const int Snack       = 1 << (int)MealType.Snack;        // 8

    public static int From(MealType type) => 1 << (int)type;
    public static bool Has(int flags, MealType type) => (flags & (1 << (int)type)) != 0;

    public static string ToLabels(int flags, string separator = " · ") =>
        string.Join(separator, Enum.GetValues<MealType>()
            .Where(t => Has(flags, t))
            .Select(t => t.ToIcon() + " " + t.ToGermanLabel()));
}

public static class MealTypeExtensions
{
    public static string ToGermanLabel(this MealType type) => type switch
    {
        MealType.Fruehstueck => "Frühstück",
        MealType.Mittagessen => "Mittagessen",
        MealType.Abendessen => "Abendessen",
        MealType.Snack => "Snack",
        _ => type.ToString()
    };

    public static string ToIcon(this MealType type) => type switch
    {
        MealType.Fruehstueck => "🌅",
        MealType.Mittagessen => "☀️",
        MealType.Abendessen => "🌙",
        MealType.Snack => "🍎",
        _ => "🍽️"
    };
}
