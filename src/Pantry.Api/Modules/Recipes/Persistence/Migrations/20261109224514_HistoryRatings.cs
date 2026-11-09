using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantry.Api.Modules.Recipes.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class HistoryRatings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "rated_at",
                schema: "recipes",
                table: "history_entries",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "rating",
                schema: "recipes",
                table: "history_entries",
                type: "integer",
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "ck_history_entries_rated_at",
                schema: "recipes",
                table: "history_entries",
                sql: "(rating IS NULL) = (rated_at IS NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "ck_history_entries_rating",
                schema: "recipes",
                table: "history_entries",
                sql: "rating IS NULL OR rating BETWEEN 1 AND 5");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_history_entries_rated_at",
                schema: "recipes",
                table: "history_entries");

            migrationBuilder.DropCheckConstraint(
                name: "ck_history_entries_rating",
                schema: "recipes",
                table: "history_entries");

            migrationBuilder.DropColumn(
                name: "rated_at",
                schema: "recipes",
                table: "history_entries");

            migrationBuilder.DropColumn(
                name: "rating",
                schema: "recipes",
                table: "history_entries");
        }
    }
}
