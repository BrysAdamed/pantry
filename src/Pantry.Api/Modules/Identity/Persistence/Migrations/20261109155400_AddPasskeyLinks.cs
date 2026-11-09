using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Pantry.Api.Modules.Identity.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPasskeyLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "passkey_links",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    token_hash = table.Column<byte[]>(type: "bytea", nullable: false),
                    member_id = table.Column<Guid>(type: "uuid", nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    expires_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    used_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true),
                    revoked_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_passkey_links", x => x.id);
                    table.CheckConstraint("ck_passkey_links_target", "(kind = 'invite') = (member_id IS NOT NULL) AND (kind = 'recovery') = (user_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "fk_passkey_links_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_passkey_links_users_user_id",
                        column: x => x.user_id,
                        principalSchema: "identity",
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_passkey_links_created_by_user_id",
                schema: "identity",
                table: "passkey_links",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_passkey_links_member_id",
                schema: "identity",
                table: "passkey_links",
                column: "member_id",
                unique: true,
                filter: "used_at IS NULL AND revoked_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_passkey_links_token_hash",
                schema: "identity",
                table: "passkey_links",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_passkey_links_user_id",
                schema: "identity",
                table: "passkey_links",
                column: "user_id",
                unique: true,
                filter: "used_at IS NULL AND revoked_at IS NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "passkey_links",
                schema: "identity");
        }
    }
}
