using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantry.Api.Modules.Household.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class PlanningRulesAndContentLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "content_language",
                schema: "household",
                table: "households",
                type: "character varying(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "planning_rules",
                schema: "household",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    text = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_planning_rules", x => x.id);
                });

            migrationBuilder.UpdateData(
                schema: "household",
                table: "households",
                keyColumn: "id",
                keyValue: 1,
                column: "content_language",
                value: null);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "planning_rules",
                schema: "household");

            migrationBuilder.DropColumn(
                name: "content_language",
                schema: "household",
                table: "households");
        }
    }
}
