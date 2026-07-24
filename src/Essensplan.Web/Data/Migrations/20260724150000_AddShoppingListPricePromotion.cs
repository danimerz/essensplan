using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Essensplan.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShoppingListPricePromotion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPromotion",
                table: "ShoppingListItems",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Price",
                table: "ShoppingListItems",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPromotion",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "Price",
                table: "ShoppingListItems");
        }
    }
}
