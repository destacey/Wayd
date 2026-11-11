using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddDemandProjectLifecycleCategory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LifecycleCategory",
                schema: "DemandManagement",
                table: "DemandProjects",
                type: "varchar(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "NotStarted");

            // Each existing copy takes its project's lifecycle category, read the way
            // ProjectStatusLifecycle.CategoryOf reads it, so outcomes report delivery from the first read rather
            // than after the next Projects Sync. The group is stamped with the time of the copy, as a resync would
            // stamp it, so an older ProjectStatusChangedEventV2 still in the outbox cannot roll it back.
            migrationBuilder.Sql(@"
                DECLARE @asOf varchar(64) = CONVERT(varchar(30), SYSUTCDATETIME(), 126) + 'Z';

                UPDATE c
                SET c.[LifecycleCategory] = CASE p.[Status]
                        WHEN 'Active' THEN 'Active'
                        WHEN 'Completed' THEN 'Completed'
                        WHEN 'Canceled' THEN 'Canceled'
                        ELSE 'NotStarted'
                    END,
                    c.[Watermarks] = JSON_MODIFY(c.[Watermarks], '$.Lifecycle', @asOf)
                FROM [DemandManagement].[DemandProjects] c
                INNER JOIN [Ppm].[Projects] p ON p.[Id] = c.[Id];");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LifecycleCategory",
                schema: "DemandManagement",
                table: "DemandProjects");
        }
    }
}
