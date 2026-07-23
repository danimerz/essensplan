using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Essensplan.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMenuAllowedMealTypes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "MealType",
                table: "Menus",
                newName: "AllowedMealTypes");

            // Convert single MealType enum value (0-3) to bitmask: bit = 1 << old_value
            migrationBuilder.Sql("UPDATE `Menus` SET `AllowedMealTypes` = 1 << `AllowedMealTypes`");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Convert bitmask back to single MealType (lossy for multi-type menus: picks highest-priority bit)
            migrationBuilder.Sql(@"
                UPDATE `Menus` SET `AllowedMealTypes` = CASE
                    WHEN (`AllowedMealTypes` & 1) != 0 THEN 0
                    WHEN (`AllowedMealTypes` & 2) != 0 THEN 1
                    WHEN (`AllowedMealTypes` & 4) != 0 THEN 2
                    WHEN (`AllowedMealTypes` & 8) != 0 THEN 3
                    ELSE 1
                END");

            migrationBuilder.RenameColumn(
                name: "AllowedMealTypes",
                table: "Menus",
                newName: "MealType");
        }
    }
}
