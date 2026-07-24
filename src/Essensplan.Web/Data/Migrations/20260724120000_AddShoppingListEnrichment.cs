using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Essensplan.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddShoppingListEnrichment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "ShoppingListItems",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "ProductCategory",
                table: "ShoppingListItems",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "ShoppingListItems");

            migrationBuilder.DropColumn(
                name: "ProductCategory",
                table: "ShoppingListItems");
        }
    }
}
