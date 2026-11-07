using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureFlagModules : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsSystem",
                schema: "FeatureManagement",
                table: "FeatureFlags");

            migrationBuilder.AddColumn<bool>(
                name: "IsModuleSwitch",
                schema: "FeatureManagement",
                table: "FeatureFlags",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // A placeholder that parses as a FeatureFlagModule; FeatureFlagSeeder sets each flag's real
            // module from its definition on the next startup.
            migrationBuilder.AddColumn<string>(
                name: "Module",
                schema: "FeatureManagement",
                table: "FeatureFlags",
                type: "nvarchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "Platform");

            // Flags can no longer be created or archived in the app, so stored roles would otherwise keep
            // permissions the application no longer defines.
            migrationBuilder.Sql("""
                DELETE FROM [Identity].[RoleClaims]
                WHERE [ClaimType] = 'permission'
                  AND [ClaimValue] IN (
                      'Permissions.FeatureFlags.Create',
                      'Permissions.FeatureFlags.Delete');
                """);
        }

        /// <inheritdoc />
        /// <remarks>The removed permission claims are not restored.</remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Module",
                schema: "FeatureManagement",
                table: "FeatureFlags");

            migrationBuilder.DropColumn(
                name: "IsModuleSwitch",
                schema: "FeatureManagement",
                table: "FeatureFlags");

            migrationBuilder.AddColumn<bool>(
                name: "IsSystem",
                schema: "FeatureManagement",
                table: "FeatureFlags",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }
    }
}
