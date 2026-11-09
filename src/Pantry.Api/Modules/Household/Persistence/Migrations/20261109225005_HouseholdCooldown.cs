using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantry.Api.Modules.Household.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HouseholdCooldown : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cooldown_days",
                schema: "household",
                table: "households",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.UpdateData(
                schema: "household",
                table: "households",
                keyColumn: "id",
                keyValue: 1,
                column: "cooldown_days",
                value: 21);

            migrationBuilder.AddCheckConstraint(
                name: "ck_households_cooldown_days",
                schema: "household",
                table: "households",
                sql: "cooldown_days BETWEEN 0 AND 365");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_households_cooldown_days",
                schema: "household",
                table: "households");

            migrationBuilder.DropColumn(
                name: "cooldown_days",
                schema: "household",
                table: "households");
        }
    }
}
