using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class GateLinksByOwner : Migration
    {
        /// <summary>
        /// Names each link's owner type from the record its owner id belongs to. A link was stored against a bare id,
        /// so the kind of record is found by looking the id up in each table a link could be attached from.
        /// </summary>
        /// <remarks>
        /// Soft-deleted records are passed over, so their links are left unnamed and removed by
        /// <see cref="RemoveOrphansSql"/>, as a deleted record's links now are. A sprint is an iteration of the sprint
        /// type; no other iteration carried links.
        /// </remarks>
        public const string NameOwnerTypesSql = """
            UPDATE l SET [OwnerType] = 'organization.team'
            FROM [Links].[Links] l
            WHERE l.[OwnerType] = '' AND EXISTS (SELECT 1 FROM [Organization].[Teams] o WHERE o.[Id] = l.[OwnerId] AND o.[IsDeleted] = 0);

            UPDATE l SET [OwnerType] = 'planning.planning-interval'
            FROM [Links].[Links] l
            WHERE l.[OwnerType] = '' AND EXISTS (SELECT 1 FROM [Planning].[PlanningIntervals] o WHERE o.[Id] = l.[OwnerId] AND o.[IsDeleted] = 0);

            UPDATE l SET [OwnerType] = 'planning.planning-interval-objective'
            FROM [Links].[Links] l
            WHERE l.[OwnerType] = '' AND EXISTS (SELECT 1 FROM [Planning].[PlanningIntervalObjectives] o WHERE o.[Id] = l.[OwnerId] AND o.[IsDeleted] = 0);

            UPDATE l SET [OwnerType] = 'planning.risk'
            FROM [Links].[Links] l
            WHERE l.[OwnerType] = '' AND EXISTS (SELECT 1 FROM [Planning].[Risks] o WHERE o.[Id] = l.[OwnerId] AND o.[IsDeleted] = 0);

            UPDATE l SET [OwnerType] = 'ppm.portfolio'
            FROM [Links].[Links] l
            WHERE l.[OwnerType] = '' AND EXISTS (SELECT 1 FROM [Ppm].[Portfolios] o WHERE o.[Id] = l.[OwnerId]);

            UPDATE l SET [OwnerType] = 'ppm.program'
            FROM [Links].[Links] l
            WHERE l.[OwnerType] = '' AND EXISTS (SELECT 1 FROM [Ppm].[Programs] o WHERE o.[Id] = l.[OwnerId]);

            UPDATE l SET [OwnerType] = 'ppm.project'
            FROM [Links].[Links] l
            WHERE l.[OwnerType] = '' AND EXISTS (SELECT 1 FROM [Ppm].[Projects] o WHERE o.[Id] = l.[OwnerId]);

            UPDATE l SET [OwnerType] = 'ppm.strategic-initiative'
            FROM [Links].[Links] l
            WHERE l.[OwnerType] = '' AND EXISTS (SELECT 1 FROM [Ppm].[StrategicInitiatives] o WHERE o.[Id] = l.[OwnerId]);

            UPDATE l SET [OwnerType] = 'product.incident'
            FROM [Links].[Links] l
            WHERE l.[OwnerType] = '' AND EXISTS (SELECT 1 FROM [ProductManagement].[Incidents] o WHERE o.[Id] = l.[OwnerId]);

            UPDATE l SET [OwnerType] = 'work.sprint'
            FROM [Links].[Links] l
            WHERE l.[OwnerType] = '' AND EXISTS (SELECT 1 FROM [Work].[Iterations] o WHERE o.[Id] = l.[OwnerId] AND o.[Type] = 'Sprint');
            """;

        /// <summary>
        /// Removes the links whose record no longer exists: nothing can show them, and nothing could say who may
        /// change them.
        /// </summary>
        public const string RemoveOrphansSql = """
            DELETE FROM [Links].[Links] WHERE [OwnerType] = '';
            """;

        /// <summary>
        /// Gives an address saved without a scheme the <c>https://</c> the page used to assume when it opened one.
        /// </summary>
        /// <remarks>
        /// The three patterns are the old page's test for an address that already had a scheme: three to five
        /// characters before <c>://</c>. An address it opened as written is left as written, so one with a scheme
        /// other than <c>http</c> or <c>https</c> stays stored, and can be renamed but not given another such address.
        /// </remarks>
        public const string NormalizeUrlsSql = """
            UPDATE [Links].[Links]
            SET [Url] = N'https://' + LTRIM([Url])
            WHERE [Url] NOT LIKE N'___://%'
              AND [Url] NOT LIKE N'____://%'
              AND [Url] NOT LIKE N'_____://%';
            """;

        /// <summary>
        /// Removes the four link permissions from every role. A record's links are now read and changed by whoever may
        /// read and edit the record, so the permissions no longer exist and roles are given nothing in their place.
        /// </summary>
        public const string RemoveLinkPermissionsSql = """
            DELETE FROM [Identity].[RoleClaims]
            WHERE [ClaimType] = 'permission'
              AND [ClaimValue] IN (
                  'Permissions.Links.View',
                  'Permissions.Links.Create',
                  'Permissions.Links.Update',
                  'Permissions.Links.Delete');
            """;

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Links_Id",
                schema: "Links",
                table: "Links");

            migrationBuilder.DropIndex(
                name: "IX_Links_ObjectId",
                schema: "Links",
                table: "Links");

            migrationBuilder.RenameColumn(
                name: "ObjectId",
                schema: "Links",
                table: "Links",
                newName: "OwnerId");

            migrationBuilder.AddColumn<string>(
                name: "OwnerType",
                schema: "Links",
                table: "Links",
                type: "varchar(64)",
                unicode: false,
                maxLength: 64,
                nullable: false,
                defaultValue: "");

            migrationBuilder.Sql(NameOwnerTypesSql);
            migrationBuilder.Sql(RemoveOrphansSql);
            migrationBuilder.Sql(NormalizeUrlsSql);
            migrationBuilder.Sql(RemoveLinkPermissionsSql);

            migrationBuilder.CreateIndex(
                name: "IX_Links_OwnerType_OwnerId",
                schema: "Links",
                table: "Links",
                columns: new[] { "OwnerType", "OwnerId" });
        }

        /// <inheritdoc />
        /// <remarks>
        /// Restores the columns only. The links removed with their records, the addresses given a scheme and the
        /// retired permissions do not come back.
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Links_OwnerType_OwnerId",
                schema: "Links",
                table: "Links");

            migrationBuilder.DropColumn(
                name: "OwnerType",
                schema: "Links",
                table: "Links");

            migrationBuilder.RenameColumn(
                name: "OwnerId",
                schema: "Links",
                table: "Links",
                newName: "ObjectId");

            migrationBuilder.CreateIndex(
                name: "IX_Links_Id",
                schema: "Links",
                table: "Links",
                column: "Id")
                .Annotation("SqlServer:Include", new[] { "ObjectId", "Name", "Url" });

            migrationBuilder.CreateIndex(
                name: "IX_Links_ObjectId",
                schema: "Links",
                table: "Links",
                column: "ObjectId")
                .Annotation("SqlServer:Include", new[] { "Id", "Name", "Url" });
        }
    }
}
