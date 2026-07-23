namespace Essensplan.Web.Models;

public enum MealType
{
    Fruehstueck = 0,
    Mittagessen = 1,
    Abendessen = 2,
    Snack = 3
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
