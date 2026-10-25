using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddOAuthTokenScopes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Scope",
                schema: "Identity",
                table: "UserRefreshTokens",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Scope",
                schema: "Identity",
                table: "OAuthAuthorizations",
                type: "nvarchar(16)",
                maxLength: 16,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Scope",
                schema: "Identity",
                table: "UserRefreshTokens");

            migrationBuilder.DropColumn(
                name: "Scope",
                schema: "Identity",
                table: "OAuthAuthorizations");
        }
    }
}
