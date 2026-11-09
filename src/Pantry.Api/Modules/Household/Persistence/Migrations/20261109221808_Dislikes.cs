using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantry.Api.Modules.Household.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Dislikes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "dislikes",
                schema: "household",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    ingredient_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_dislikes", x => x.id);
                    table.CheckConstraint("ck_dislikes_kind", "kind IN ('never', 'avoid')");
                    table.CheckConstraint("ck_dislikes_reason", "reason IS NULL OR kind = 'never'");
                });

            migrationBuilder.CreateIndex(
                name: "ix_dislikes_ingredient_id",
                schema: "household",
                table: "dislikes",
                column: "ingredient_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dislikes",
                schema: "household");
        }
    }
}
