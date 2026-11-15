using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace Pantrio.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:pg_trgm", ",,");

            migrationBuilder.CreateTable(
                name: "ai_usage",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    purpose = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    model = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    input_tokens = table.Column<int>(type: "integer", nullable: false),
                    output_tokens = table.Column<int>(type: "integer", nullable: false),
                    succeeded = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ai_usage", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "app_user",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    avatar_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    last_sign_in_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_app_user", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "data_protection_key",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    friendly_name = table.Column<string>(type: "text", nullable: true),
                    xml = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_data_protection_key", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "household",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    time_zone = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    dietary_prefs = table.Column<List<string>>(type: "text[]", nullable: false),
                    hidden_meal_slots = table.Column<string[]>(type: "text[]", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_household", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "user_login",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    subject = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_login", x => x.id);
                    table.ForeignKey(
                        name: "fk_user_login_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "household_member",
                columns: table => new
                {
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    can_delete = table.Column<bool>(type: "boolean", nullable: false),
                    joined_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_household_member", x => new { x.household_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_household_member_app_user_user_id",
                        column: x => x.user_id,
                        principalTable: "app_user",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_household_member_household_household_id",
                        column: x => x.household_id,
                        principalTable: "household",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ingredient",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    normalized_name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    aliases = table.Column<List<string>>(type: "text[]", nullable: false),
                    default_unit = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    is_staple = table.Column<bool>(type: "boolean", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_ingredient", x => x.id);
                    table.ForeignKey(
                        name: "fk_ingredient_household_household_id",
                        column: x => x.household_id,
                        principalTable: "household",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meal_plan",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    week_start = table.Column<DateOnly>(type: "date", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meal_plan", x => x.id);
                    table.ForeignKey(
                        name: "fk_meal_plan_household_household_id",
                        column: x => x.household_id,
                        principalTable: "household",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipe",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    servings = table.Column<int>(type: "integer", nullable: false),
                    tags = table.Column<List<string>>(type: "text[]", nullable: false),
                    prep_mins = table.Column<int>(type: "integer", nullable: true),
                    notes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    method_text = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    method_source = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    source_url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    is_favourite = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipe", x => x.id);
                    table.ForeignKey(
                        name: "fk_recipe_household_household_id",
                        column: x => x.household_id,
                        principalTable: "household",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "shopping_list_item",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingredient_id = table.Column<Guid>(type: "uuid", nullable: true),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    dimension = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    unit_label = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    category = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    state = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    is_extra = table.Column<bool>(type: "boolean", nullable: false),
                    extra_quantity_text = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    in_check_group = table.Column<bool>(type: "boolean", nullable: false),
                    bought_total = table.Column<decimal>(type: "numeric", nullable: true),
                    amount_increased = table.Column<bool>(type: "boolean", nullable: false),
                    added_by = table.Column<Guid>(type: "uuid", nullable: true),
                    checked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_shopping_list_item", x => x.id);
                    table.ForeignKey(
                        name: "fk_shopping_list_item_household_household_id",
                        column: x => x.household_id,
                        principalTable: "household",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_shopping_list_item_ingredient_ingredient_id",
                        column: x => x.ingredient_id,
                        principalTable: "ingredient",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "meal_plan_entry",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day = table.Column<DateOnly>(type: "date", nullable: true),
                    meal_slot = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    servings_override = table.Column<int>(type: "integer", nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    added_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_meal_plan_entry", x => x.id);
                    table.CheckConstraint("ck_meal_plan_entry_slot", "(day IS NULL AND meal_slot IS NULL) OR (day IS NOT NULL AND meal_slot IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_meal_plan_entry_meal_plan_meal_plan_id",
                        column: x => x.meal_plan_id,
                        principalTable: "meal_plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_meal_plan_entry_recipe_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipe",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "recipe_ingredient",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingredient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: true),
                    unit = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: true),
                    quantity_text = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    optional = table.Column<bool>(type: "boolean", nullable: false),
                    note = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_recipe_ingredient", x => x.id);
                    table.ForeignKey(
                        name: "fk_recipe_ingredient_ingredient_ingredient_id",
                        column: x => x.ingredient_id,
                        principalTable: "ingredient",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_recipe_ingredient_recipe_recipe_id",
                        column: x => x.recipe_id,
                        principalTable: "recipe",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "list_item_source",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false),
                    list_item_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_plan_id = table.Column<Guid>(type: "uuid", nullable: false),
                    meal_plan_entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    recipe_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    quantity = table.Column<decimal>(type: "numeric", nullable: true),
                    quantity_text = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    cleared = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_list_item_source", x => x.id);
                    table.ForeignKey(
                        name: "fk_list_item_source_meal_plan_meal_plan_id",
                        column: x => x.meal_plan_id,
                        principalTable: "meal_plan",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_list_item_source_shopping_list_item_list_item_id",
                        column: x => x.list_item_id,
                        principalTable: "shopping_list_item",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_created_at",
                table: "ai_usage",
                column: "created_at");

            migrationBuilder.CreateIndex(
                name: "ix_ai_usage_household_id_created_at",
                table: "ai_usage",
                columns: new[] { "household_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "ix_app_user_email",
                table: "app_user",
                column: "email");

            migrationBuilder.CreateIndex(
                name: "ix_household_member_user_id",
                table: "household_member",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_ingredient_household_id_normalized_name",
                table: "ingredient",
                columns: new[] { "household_id", "normalized_name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_ingredient_normalized_name",
                table: "ingredient",
                column: "normalized_name")
                .Annotation("Npgsql:IndexMethod", "gin")
                .Annotation("Npgsql:IndexOperators", new[] { "gin_trgm_ops" });

            migrationBuilder.CreateIndex(
                name: "ix_list_item_source_list_item_id_meal_plan_id_meal_plan_entry_",
                table: "list_item_source",
                columns: new[] { "list_item_id", "meal_plan_id", "meal_plan_entry_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_list_item_source_meal_plan_id",
                table: "list_item_source",
                column: "meal_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_meal_plan_household_id_week_start",
                table: "meal_plan",
                columns: new[] { "household_id", "week_start" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_meal_plan_entry_meal_plan_id",
                table: "meal_plan_entry",
                column: "meal_plan_id");

            migrationBuilder.CreateIndex(
                name: "ix_meal_plan_entry_recipe_id",
                table: "meal_plan_entry",
                column: "recipe_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipe_household_id_name",
                table: "recipe",
                columns: new[] { "household_id", "name" });

            migrationBuilder.CreateIndex(
                name: "ix_recipe_ingredient_ingredient_id",
                table: "recipe_ingredient",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "ix_recipe_ingredient_recipe_id_sort_order",
                table: "recipe_ingredient",
                columns: new[] { "recipe_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_shopping_list_item_household_id_ingredient_id_dimension_uni",
                table: "shopping_list_item",
                columns: new[] { "household_id", "ingredient_id", "dimension", "unit_label" },
                unique: true,
                filter: "ingredient_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_shopping_list_item_ingredient_id",
                table: "shopping_list_item",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "ix_user_login_provider_subject",
                table: "user_login",
                columns: new[] { "provider", "subject" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_user_login_user_id",
                table: "user_login",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_usage");

            migrationBuilder.DropTable(
                name: "data_protection_key");

            migrationBuilder.DropTable(
                name: "household_member");

            migrationBuilder.DropTable(
                name: "list_item_source");

            migrationBuilder.DropTable(
                name: "meal_plan_entry");

            migrationBuilder.DropTable(
                name: "recipe_ingredient");

            migrationBuilder.DropTable(
                name: "user_login");

            migrationBuilder.DropTable(
                name: "shopping_list_item");

            migrationBuilder.DropTable(
                name: "meal_plan");

            migrationBuilder.DropTable(
                name: "recipe");

            migrationBuilder.DropTable(
                name: "app_user");

            migrationBuilder.DropTable(
                name: "ingredient");

            migrationBuilder.DropTable(
                name: "household");
        }
    }
}
