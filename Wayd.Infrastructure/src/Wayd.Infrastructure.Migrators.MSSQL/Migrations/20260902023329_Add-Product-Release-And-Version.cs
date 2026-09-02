using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations
{
    /// <inheritdoc />
    public partial class AddProductReleaseAndVersion : Migration
    {
        /// <summary>
        /// The timestamp stamped on rows this migration authors.
        /// </summary>
        /// <remarks>
        /// A fixed literal in the invariant round-trip shape rather than a formatted <c>DateTime.UtcNow</c>:
        /// migrations have no <c>IDateTimeProvider</c>, and the current culture's default format is not
        /// parseable by SQL Server on non-Windows hosts — ICU 72+ emits U+202F before AM/PM, which fails
        /// with error 241. Being fixed also makes the migration produce the same SQL wherever it runs.
        /// </remarks>
        private const string MigratedOn = "2026-09-02T00:00:00.000";

        /// <inheritdoc />
        /// <remarks>
        /// Splits the delivery record that shipped as "Release" into the two things it was doing at
        /// once: <c>Delivery.Versions</c> is the artifact that was cut, and <c>Delivery.Releases</c>
        /// becomes what was announced to customers.
        /// <para>
        /// <strong>The existing rows are versions, and they move.</strong> Every row in
        /// <c>Delivery.Releases</c> today carries a ProductId and a CutDate and holds a cut-and-ship
        /// status history — that is an artifact, whatever the table was called. Leaving them behind
        /// would turn each into an announcement that never happened, silently dropping its cut date,
        /// and would leave the new table empty. So the rows are copied to Versions <em>keeping their
        /// ids</em>, and Releases is emptied for real announcements.
        /// </para>
        /// <para>
        /// Preserving the ids is what makes the rest cheap: <c>Deployments.VersionId</c> and
        /// <c>ReleasePackageComponents.VersionId</c> are renames of columns whose values already point
        /// at the right rows, and the status history moves with a single UPDATE of its OwnerType rather
        /// than a re-keying.
        /// </para>
        /// <para>
        /// <c>AuditTrails</c> rows written before this migration still name <c>Delivery.Releases</c>
        /// for records that are now versions. They are deliberately left alone: rewriting an audit
        /// history to say something other than what it said at the time is worse than a documented
        /// discontinuity at this migration.
        /// </para>
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Deployments_Releases_ReleaseId",
                schema: "Delivery",
                table: "Deployments");

            migrationBuilder.DropIndex(
                name: "IX_Releases_ProductId_ReleasedDate",
                schema: "Delivery",
                table: "Releases");

            migrationBuilder.DropIndex(
                name: "IX_Releases_ProductId_Version",
                schema: "Delivery",
                table: "Releases");

            migrationBuilder.DropIndex(
                name: "IX_Deployments_EnvironmentCategory_StatusAliasValue_CompletedAt",
                schema: "Delivery",
                table: "Deployments");

            // CutDate is dropped further down, after the rows have been copied into Versions — dropping
            // it here would discard the cut dates the copy exists to carry across.

            migrationBuilder.RenameColumn(
                name: "ReleaseId",
                schema: "Delivery",
                table: "ReleasePackageComponents",
                newName: "VersionId");

            migrationBuilder.RenameColumn(
                name: "ReleaseId",
                schema: "Delivery",
                table: "Deployments",
                newName: "VersionId");

            migrationBuilder.RenameIndex(
                name: "IX_Deployments_ReleaseId",
                schema: "Delivery",
                table: "Deployments",
                newName: "IX_Deployments_VersionId");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductId",
                schema: "Delivery",
                table: "Releases",
                type: "uniqueidentifier",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier");

            migrationBuilder.CreateTable(
                name: "ReleasePackageInclusions",
                schema: "Delivery",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReleaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PackageId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleasePackageInclusions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleasePackageInclusions_ReleasePackages_PackageId",
                        column: x => x.PackageId,
                        principalSchema: "Delivery",
                        principalTable: "ReleasePackages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ReleasePackageInclusions_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalSchema: "Delivery",
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Versions",
                schema: "Delivery",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Key = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Number = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    Sequence = table.Column<long>(type: "bigint", nullable: true),
                    TargetDate = table.Column<DateTime>(type: "date", nullable: true),
                    CutDate = table.Column<DateTime>(type: "date", nullable: true),
                    ReleasedDate = table.Column<DateTime>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    StatusId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatusWorkflowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StatusName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StatusCategory = table.Column<string>(type: "varchar(32)", maxLength: 32, nullable: false),
                    StatusAliasValue = table.Column<int>(type: "int", nullable: false),
                    StatusTransitionCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Versions", x => x.Id);
                    table.UniqueConstraint("AK_Versions_Key", x => x.Key);
                    table.ForeignKey(
                        name: "FK_Versions_Products_ProductId",
                        column: x => x.ProductId,
                        principalSchema: "ProductManagement",
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReleaseVersions",
                schema: "Delivery",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReleaseId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VersionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SystemCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemCreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SystemLastModified = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SystemLastModifiedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReleaseVersions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReleaseVersions_Releases_ReleaseId",
                        column: x => x.ReleaseId,
                        principalSchema: "Delivery",
                        principalTable: "Releases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ReleaseVersions_Versions_VersionId",
                        column: x => x.VersionId,
                        principalSchema: "Delivery",
                        principalTable: "Versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Releases_ProductId",
                schema: "Delivery",
                table: "Releases",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Releases_ReleasedDate",
                schema: "Delivery",
                table: "Releases",
                column: "ReleasedDate")
                .Annotation("SqlServer:Include", new[] { "Id", "Key", "Version", "Name", "Sequence", "StatusCategory" });

            migrationBuilder.CreateIndex(
                name: "IX_Deployments_EnvironmentCategory_StatusAliasValue_CompletedAt",
                schema: "Delivery",
                table: "Deployments",
                columns: new[] { "EnvironmentCategory", "StatusAliasValue", "CompletedAt" })
                .Annotation("SqlServer:Include", new[] { "Id", "Key", "VersionId", "PackageId", "EnvironmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_ReleasePackageInclusions_PackageId",
                schema: "Delivery",
                table: "ReleasePackageInclusions",
                column: "PackageId");

            migrationBuilder.CreateIndex(
                name: "IX_ReleasePackageInclusions_ReleaseId_PackageId",
                schema: "Delivery",
                table: "ReleasePackageInclusions",
                columns: new[] { "ReleaseId", "PackageId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseVersions_ReleaseId_VersionId",
                schema: "Delivery",
                table: "ReleaseVersions",
                columns: new[] { "ReleaseId", "VersionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReleaseVersions_VersionId",
                schema: "Delivery",
                table: "ReleaseVersions",
                column: "VersionId");

            migrationBuilder.CreateIndex(
                name: "IX_Versions_ProductId_Number",
                schema: "Delivery",
                table: "Versions",
                columns: new[] { "ProductId", "Number" });

            migrationBuilder.CreateIndex(
                name: "IX_Versions_ProductId_ReleasedDate",
                schema: "Delivery",
                table: "Versions",
                columns: new[] { "ProductId", "ReleasedDate" })
                .Annotation("SqlServer:Include", new[] { "Id", "Key", "Number", "Name", "Sequence", "StatusCategory" });

            migrationBuilder.CreateIndex(
                name: "IX_Versions_StatusWorkflowId",
                schema: "Delivery",
                table: "Versions",
                column: "StatusWorkflowId");

            // Every existing row becomes a version, keeping its Id and its Key.
            //
            // IDENTITY_INSERT because Key is database-generated: without it every version is renumbered,
            // which silently breaks every URL and bookmark addressing a record by its short key — the
            // one identifier a reader actually recognises.
            //
            // This runs before the Deployments foreign key below is created, so the rows those
            // deployments point at already exist when the constraint is checked.
            migrationBuilder.Sql("""
                SET IDENTITY_INSERT [Delivery].[Versions] ON;

                INSERT INTO [Delivery].[Versions] (
                    [Id], [Key], [ProductId], [Number], [Name], [Sequence],
                    [TargetDate], [CutDate], [ReleasedDate], [Notes],
                    [SystemCreated], [SystemCreatedBy], [SystemLastModified], [SystemLastModifiedBy],
                    [StatusId], [StatusWorkflowId], [StatusName], [StatusCategory],
                    [StatusAliasValue], [StatusTransitionCount])
                SELECT
                    [Id], [Key], [ProductId], [Version], [Name], [Sequence],
                    [TargetDate], [CutDate], [ReleasedDate], [Notes],
                    [SystemCreated], [SystemCreatedBy], [SystemLastModified], [SystemLastModifiedBy],
                    [StatusId], [StatusWorkflowId], [StatusName], [StatusCategory],
                    [StatusAliasValue], [StatusTransitionCount]
                FROM [Delivery].[Releases];

                SET IDENTITY_INSERT [Delivery].[Versions] OFF;
                """);

            // Reseed the identity so the next version continues from the highest key copied, rather
            // than from 1 — which would collide with every row just inserted.
            migrationBuilder.Sql("""
                DECLARE @maxKey int = (SELECT MAX([Key]) FROM [Delivery].[Versions]);
                IF @maxKey IS NOT NULL
                    DBCC CHECKIDENT ('[Delivery].[Versions]', RESEED, @maxKey);
                """);

            // The status history belongs to the record that made it. These transitions record cuts and
            // shipments of an artifact, so they move to the version owner type with the rows; leaving
            // them would attribute a cut-and-ship history to an announcement that never happened.
            //
            // A single UPDATE per table rather than a re-keying, because the ids did not change.
            //
            // All four tables that carry an OwnerType, not just the obvious two. WorkflowAssignments is
            // the one that matters most and is easiest to miss: it is how a record resolves its
            // workflow at all, so leaving it behind would let every version fail to resolve one while
            // the transitions looked correctly migrated.
            migrationBuilder.Sql("""
                UPDATE [StatusWorkflows].[StatusTransitions]
                SET [OwnerType] = 'delivery.version'
                WHERE [OwnerType] = 'delivery.release';

                UPDATE [StatusWorkflows].[StatusWorkflows]
                SET [OwnerType] = 'delivery.version'
                WHERE [OwnerType] = 'delivery.release';

                UPDATE [StatusWorkflows].[WorkflowAssignments]
                SET [OwnerType] = 'delivery.version'
                WHERE [OwnerType] = 'delivery.release';

                UPDATE [StatusWorkflows].[WorkflowAliasNames]
                SET [OwnerType] = 'delivery.version'
                WHERE [OwnerType] = 'delivery.release';
                """);

            // The seeded workflow moves with its records, so its name and description now describe the
            // wrong thing. Renamed only where it is still the untouched system default — an
            // organization that renamed it said what it wanted the workflow called.
            migrationBuilder.Sql("""
                UPDATE [StatusWorkflows].[StatusWorkflows]
                SET [Name] = 'Default Version Workflow',
                    [Description] = 'The lifecycle of a versioned cut of one product.'
                WHERE [OwnerType] = 'delivery.version'
                  AND [IsSystem] = 1
                  AND [Name] = 'Default Release Workflow';
                """);

            // Emptied for real announcements. The rows are not deleted so much as relocated — every one
            // of them is now in Versions, with the same id.
            migrationBuilder.Sql("DELETE FROM [Delivery].[Releases];");

            // Dropped only now: until the copy above ran, this column held the cut dates.
            migrationBuilder.DropColumn(
                name: "CutDate",
                schema: "Delivery",
                table: "Releases");

            // The engineering claims are superseded by one Delivery resource. Granted to any role that
            // held any of them, so nobody loses access they had; the announcement claims start
            // ungranted, which is the right default for a capability nobody has used yet.
            // Created is NOT NULL and has no default, so both audit columns are supplied here. This
            // migration is the author of the new claim, which is what the column should say.
            migrationBuilder.Sql($"""
                INSERT INTO [Identity].[RoleClaims] ([RoleId], [ClaimType], [ClaimValue], [CreatedBy], [Created])
                SELECT DISTINCT
                    rc.[RoleId],
                    'permission',
                    'Permissions.Delivery.' + a.[Action],
                    'AddProductReleaseAndVersion',
                    CAST('{MigratedOn}' AS datetime2)
                FROM [Identity].[RoleClaims] rc
                CROSS APPLY (VALUES ('View'), ('Create'), ('Update')) AS a([Action])
                WHERE rc.[ClaimType] = 'permission'
                  AND rc.[ClaimValue] = 'Permissions.Releases.' + a.[Action]
                  AND NOT EXISTS (
                      SELECT 1 FROM [Identity].[RoleClaims] existing
                      WHERE existing.[RoleId] = rc.[RoleId]
                        AND existing.[ClaimType] = 'permission'
                        AND existing.[ClaimValue] = 'Permissions.Delivery.' + a.[Action]);
                """);

            // Then remove the superseded claims, the old Releases ones included: a role that could
            // record engineering releases has not thereby been granted the right to announce a product
            // release, which is a different act for a different audience.
            migrationBuilder.Sql("""
                DELETE FROM [Identity].[RoleClaims]
                WHERE [ClaimType] = 'permission'
                  AND [ClaimValue] IN (
                      'Permissions.Releases.View',
                      'Permissions.Releases.Create',
                      'Permissions.Releases.Update',
                      'Permissions.ReleasePackages.View',
                      'Permissions.ReleasePackages.Create',
                      'Permissions.ReleasePackages.Update',
                      'Permissions.Deployments.View',
                      'Permissions.Deployments.Create',
                      'Permissions.Deployments.Update');
                """);

            migrationBuilder.AddForeignKey(
                name: "FK_Deployments_Versions_VersionId",
                schema: "Delivery",
                table: "Deployments",
                column: "VersionId",
                principalSchema: "Delivery",
                principalTable: "Versions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        /// <remarks>
        /// Restores the schema, and moves the version rows back into Releases so the records survive
        /// the rollback. Run before the tables are dropped, for the same reason the Up copy runs before
        /// its foreign key is created.
        /// <para>
        /// <strong>Not a perfect inverse.</strong> Anything created after this migration is lost, and
        /// necessarily so: a real product release, a release's versions or packages, and any version
        /// whose product node is gone have nowhere to go in the old single-table shape. The rollback is
        /// for undoing a bad deploy promptly, not for running a split database on the old schema.
        /// </para>
        /// </remarks>
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Announcements have no home in the old shape, and their contents point at rows that are
            // about to move. Cleared first so the joins drop cleanly and the Releases table is empty
            // for the versions coming back into it.
            migrationBuilder.Sql("DELETE FROM [Delivery].[ReleaseVersions];");
            migrationBuilder.Sql("DELETE FROM [Delivery].[ReleasePackageInclusions];");
            migrationBuilder.Sql("""
                DELETE FROM [StatusWorkflows].[StatusTransitions]
                WHERE [OwnerType] = 'delivery.release';
                """);
            migrationBuilder.Sql("DELETE FROM [Delivery].[Releases];");

            migrationBuilder.Sql("""
                UPDATE [StatusWorkflows].[StatusTransitions]
                SET [OwnerType] = 'delivery.release'
                WHERE [OwnerType] = 'delivery.version';

                UPDATE [StatusWorkflows].[StatusWorkflows]
                SET [OwnerType] = 'delivery.release'
                WHERE [OwnerType] = 'delivery.version';

                UPDATE [StatusWorkflows].[WorkflowAssignments]
                SET [OwnerType] = 'delivery.release'
                WHERE [OwnerType] = 'delivery.version';

                UPDATE [StatusWorkflows].[WorkflowAliasNames]
                SET [OwnerType] = 'delivery.release'
                WHERE [OwnerType] = 'delivery.version';
                """);

            migrationBuilder.Sql("""
                UPDATE [StatusWorkflows].[StatusWorkflows]
                SET [Name] = 'Default Release Workflow',
                    [Description] = 'The lifecycle of a release.'
                WHERE [OwnerType] = 'delivery.release'
                  AND [IsSystem] = 1
                  AND [Name] = 'Default Version Workflow';
                """);

            migrationBuilder.Sql($"""
                INSERT INTO [Identity].[RoleClaims] ([RoleId], [ClaimType], [ClaimValue], [CreatedBy], [Created])
                SELECT DISTINCT
                    rc.[RoleId],
                    'permission',
                    p.[Prefix] + a.[Action],
                    'AddProductReleaseAndVersion',
                    CAST('{MigratedOn}' AS datetime2)
                FROM [Identity].[RoleClaims] rc
                CROSS APPLY (VALUES ('View'), ('Create'), ('Update')) AS a([Action])
                CROSS APPLY (VALUES
                    ('Permissions.Releases.'),
                    ('Permissions.ReleasePackages.'),
                    ('Permissions.Deployments.')) AS p([Prefix])
                WHERE rc.[ClaimType] = 'permission'
                  AND rc.[ClaimValue] = 'Permissions.Delivery.' + a.[Action]
                  AND NOT EXISTS (
                      SELECT 1 FROM [Identity].[RoleClaims] existing
                      WHERE existing.[RoleId] = rc.[RoleId]
                        AND existing.[ClaimType] = 'permission'
                        AND existing.[ClaimValue] = p.[Prefix] + a.[Action]);

                DELETE FROM [Identity].[RoleClaims]
                WHERE [ClaimType] = 'permission'
                  AND [ClaimValue] IN (
                      'Permissions.Delivery.View',
                      'Permissions.Delivery.Create',
                      'Permissions.Delivery.Update');
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_Deployments_Versions_VersionId",
                schema: "Delivery",
                table: "Deployments");

            migrationBuilder.DropTable(
                name: "ReleasePackageInclusions",
                schema: "Delivery");

            migrationBuilder.DropTable(
                name: "ReleaseVersions",
                schema: "Delivery");

            // The new indexes go first: SQL Server refuses to alter a column an index depends on, and
            // ProductId is about to become non-nullable again.
            migrationBuilder.DropIndex(
                name: "IX_Releases_ProductId",
                schema: "Delivery",
                table: "Releases");

            migrationBuilder.DropIndex(
                name: "IX_Releases_ReleasedDate",
                schema: "Delivery",
                table: "Releases");

            // CutDate comes back, and ProductId returns to non-nullable, before the rows are copied
            // home — the copy needs both columns present, and Releases is empty at this point so
            // tightening the nullability cannot fail on existing data.
            migrationBuilder.AddColumn<DateTime>(
                name: "CutDate",
                schema: "Delivery",
                table: "Releases",
                type: "date",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductId",
                schema: "Delivery",
                table: "Releases",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uniqueidentifier",
                oldNullable: true);

            // The versions move back into Releases, keeping their ids and keys, so the Deployments and
            // ReleasePackageComponents columns renamed below still point at real rows.
            migrationBuilder.Sql("""
                SET IDENTITY_INSERT [Delivery].[Releases] ON;

                INSERT INTO [Delivery].[Releases] (
                    [Id], [Key], [ProductId], [Version], [Name], [Sequence],
                    [TargetDate], [CutDate], [ReleasedDate], [Notes],
                    [SystemCreated], [SystemCreatedBy], [SystemLastModified], [SystemLastModifiedBy],
                    [StatusId], [StatusWorkflowId], [StatusName], [StatusCategory],
                    [StatusAliasValue], [StatusTransitionCount])
                SELECT
                    [Id], [Key], [ProductId], [Number], [Name], [Sequence],
                    [TargetDate], [CutDate], [ReleasedDate], [Notes],
                    [SystemCreated], [SystemCreatedBy], [SystemLastModified], [SystemLastModifiedBy],
                    [StatusId], [StatusWorkflowId], [StatusName], [StatusCategory],
                    [StatusAliasValue], [StatusTransitionCount]
                FROM [Delivery].[Versions];

                SET IDENTITY_INSERT [Delivery].[Releases] OFF;
                """);

            migrationBuilder.Sql("""
                DECLARE @maxKey int = (SELECT MAX([Key]) FROM [Delivery].[Releases]);
                IF @maxKey IS NOT NULL
                    DBCC CHECKIDENT ('[Delivery].[Releases]', RESEED, @maxKey);
                """);

            migrationBuilder.DropTable(
                name: "Versions",
                schema: "Delivery");

            // The two Releases indexes were dropped earlier, before ProductId was altered.
            migrationBuilder.DropIndex(
                name: "IX_Deployments_EnvironmentCategory_StatusAliasValue_CompletedAt",
                schema: "Delivery",
                table: "Deployments");

            migrationBuilder.RenameColumn(
                name: "VersionId",
                schema: "Delivery",
                table: "ReleasePackageComponents",
                newName: "ReleaseId");

            migrationBuilder.RenameColumn(
                name: "VersionId",
                schema: "Delivery",
                table: "Deployments",
                newName: "ReleaseId");

            migrationBuilder.RenameIndex(
                name: "IX_Deployments_VersionId",
                schema: "Delivery",
                table: "Deployments",
                newName: "IX_Deployments_ReleaseId");

            // ProductId and CutDate were restored above, before the rows were copied home.

            migrationBuilder.CreateIndex(
                name: "IX_Releases_ProductId_ReleasedDate",
                schema: "Delivery",
                table: "Releases",
                columns: new[] { "ProductId", "ReleasedDate" })
                .Annotation("SqlServer:Include", new[] { "Id", "Key", "Version", "Name", "Sequence", "StatusCategory" });

            migrationBuilder.CreateIndex(
                name: "IX_Releases_ProductId_Version",
                schema: "Delivery",
                table: "Releases",
                columns: new[] { "ProductId", "Version" });

            migrationBuilder.CreateIndex(
                name: "IX_Deployments_EnvironmentCategory_StatusAliasValue_CompletedAt",
                schema: "Delivery",
                table: "Deployments",
                columns: new[] { "EnvironmentCategory", "StatusAliasValue", "CompletedAt" })
                .Annotation("SqlServer:Include", new[] { "Id", "Key", "ReleaseId", "PackageId", "EnvironmentId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Deployments_Releases_ReleaseId",
                schema: "Delivery",
                table: "Deployments",
                column: "ReleaseId",
                principalSchema: "Delivery",
                principalTable: "Releases",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
