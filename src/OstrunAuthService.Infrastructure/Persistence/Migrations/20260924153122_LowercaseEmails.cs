using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OstrunAuthService.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class LowercaseEmails : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Fails on the unique index if two rows differ only by case. That
            // conflict needs a human decision on which account to keep.
            migrationBuilder.Sql("UPDATE users SET \"Email\" = lower(\"Email\") WHERE \"Email\" <> lower(\"Email\");");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The original casing is not recoverable.
        }
    }
}
