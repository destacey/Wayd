using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddOidcProviderRegistrationPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowAutoRegistration",
                schema: "Identity",
                table: "OidcProviders",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DefaultRoleId",
                schema: "Identity",
                table: "OidcProviders",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequireEmployeeRecord",
                schema: "Identity",
                table: "OidcProviders",
                type: "bit",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_OidcProviders_DefaultRoleId",
                schema: "Identity",
                table: "OidcProviders",
                column: "DefaultRoleId");

            migrationBuilder.AddForeignKey(
                name: "FK_OidcProviders_Roles_DefaultRoleId",
                schema: "Identity",
                table: "OidcProviders",
                column: "DefaultRoleId",
                principalSchema: "Identity",
                principalTable: "Roles",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OidcProviders_Roles_DefaultRoleId",
                schema: "Identity",
                table: "OidcProviders");

            migrationBuilder.DropIndex(
                name: "IX_OidcProviders_DefaultRoleId",
                schema: "Identity",
                table: "OidcProviders");

            migrationBuilder.DropColumn(
                name: "AllowAutoRegistration",
                schema: "Identity",
                table: "OidcProviders");

            migrationBuilder.DropColumn(
                name: "DefaultRoleId",
                schema: "Identity",
                table: "OidcProviders");

            migrationBuilder.DropColumn(
                name: "RequireEmployeeRecord",
                schema: "Identity",
                table: "OidcProviders");
        }
    }
}
