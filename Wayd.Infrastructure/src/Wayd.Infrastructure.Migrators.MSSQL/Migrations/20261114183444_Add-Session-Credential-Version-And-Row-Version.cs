using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionCredentialVersionAndRowVersion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CredentialVersion",
                schema: "Identity",
                table: "UserRefreshTokens",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RotatedFromIpAddress",
                schema: "Identity",
                table: "UserRefreshTokens",
                type: "nvarchar(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "Version",
                schema: "Identity",
                table: "UserRefreshTokens",
                type: "rowversion",
                rowVersion: true,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CredentialVersion",
                schema: "Identity",
                table: "UserRefreshTokens");

            migrationBuilder.DropColumn(
                name: "RotatedFromIpAddress",
                schema: "Identity",
                table: "UserRefreshTokens");

            migrationBuilder.DropColumn(
                name: "Version",
                schema: "Identity",
                table: "UserRefreshTokens");
        }
    }
}
