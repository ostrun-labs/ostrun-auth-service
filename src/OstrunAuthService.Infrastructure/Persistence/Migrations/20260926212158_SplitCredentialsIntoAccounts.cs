using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OstrunAuthService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class SplitCredentialsIntoAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "EmailVerified",
                table: "users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Image",
                table: "users",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Name",
                table: "users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "accounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProviderId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    ProviderAccountId = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_accounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_accounts_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_accounts_ProviderId_ProviderAccountId",
                table: "accounts",
                columns: new[] { "ProviderId", "ProviderAccountId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_accounts_UserId",
                table: "accounts",
                column: "UserId");

            // Every existing user signed up with a password: move each hash
            // into a credential account before dropping the column.
            migrationBuilder.Sql("""
                INSERT INTO accounts ("Id", "UserId", "ProviderId", "ProviderAccountId", "PasswordHash", "CreatedAt")
                SELECT gen_random_uuid(), "Id", 'credential', "Id"::text, "PasswordHash", "CreatedAt" FROM users;
                """);

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "users",
                type: "text",
                nullable: false,
                defaultValue: "");

            // Users without a credential account (social-only) keep an empty
            // hash, which never verifies.
            migrationBuilder.Sql("""
                UPDATE users u SET "PasswordHash" = a."PasswordHash"
                FROM accounts a
                WHERE a."UserId" = u."Id" AND a."ProviderId" = 'credential';
                """);

            migrationBuilder.DropTable(
                name: "accounts");

            migrationBuilder.DropColumn(
                name: "EmailVerified",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Image",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Name",
                table: "users");
        }
    }
}
