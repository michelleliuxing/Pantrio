using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantrio.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class IngredientPurchaseUrlAndRecipeImage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "purchase_url",
                table: "ingredient",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "recipe_image",
                columns: table => new
                {
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    content_type = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    data = table.Column<byte[]>(type: "bytea", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipe_image", x => x.recipe_id);
                    table.ForeignKey(
                        name: "fk_recipe_image_recipe_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipe",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recipe_image");

            migrationBuilder.DropColumn(
                name: "purchase_url",
                table: "ingredient");
        }
    }
}
