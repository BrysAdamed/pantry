using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantry.Api.Modules.Household.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialHousehold : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "household");

            migrationBuilder.CreateTable(
                name: "households",
                schema: "household",
                columns: table => new
                {
                    id = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_households", x => x.id);
                    table.CheckConstraint("ck_households_singleton", "id = 1");
                });

            migrationBuilder.CreateTable(
                name: "members",
                schema: "household",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    portion_factor = table.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
                    position = table.Column<int>(type: "integer", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_members", x => x.id);
                    table.CheckConstraint("ck_members_portion_factor", "portion_factor >= 0.05 AND portion_factor <= 2.00");
                });

            migrationBuilder.InsertData(
                schema: "household",
                table: "households",
                column: "id",
                value: 1);

            migrationBuilder.CreateIndex(
                name: "ix_members_user_id",
                schema: "household",
                table: "members",
                column: "user_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "households",
                schema: "household");

            migrationBuilder.DropTable(
                name: "members",
                schema: "household");
        }
    }
}
