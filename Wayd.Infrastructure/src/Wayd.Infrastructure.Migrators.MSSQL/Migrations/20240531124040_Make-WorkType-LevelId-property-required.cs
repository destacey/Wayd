using System.Globalization;
using Microsoft.EntityFrameworkCore.Migrations;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Work;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class MakeWorkTypeLevelIdpropertyrequired : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        var defaultTier = WorkTypeTier.Other.ToString();
        var ownershipId = (int)Ownership.System;
        // Format invariantly: these values are interpolated straight into SQL, and the
        // current culture's default DateTime format is not round-trippable by SQL Server.
        // ICU 72+ emits U+202F (narrow no-break space) before AM/PM for en-US, which fails
        // conversion with error 241 on every non-Windows host.
        var timestamp = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fff", CultureInfo.InvariantCulture);

        var hierarchyId = migrationBuilder.Sql($@"
            INSERT INTO [Work].[WorkTypeHierarchies] ([SystemCreated], [SystemLastModified], [SystemCreatedBy], [SystemLastModifiedBy])
            VALUES ('{timestamp}', '{timestamp}', '{Guid.Empty}', '{Guid.Empty}')");

        migrationBuilder.Sql($@"
            INSERT INTO [Work].[WorkTypeLevels] ([Name], [Description], [Tier], [Ownership], [Order], [WorkTypeHierarchyId], [SystemCreated], [SystemLastModified], [SystemCreatedBy], [SystemLastModifiedBy])
            VALUES ('Other', 
                'A tier for non-standard work types.  Work Types in this tier will not appear in backlog or iteration views.  It is used for special work types.  This is also the default tier for new work types.', 
                '{defaultTier}', 
                {ownershipId}, 
                1, 
                (SELECT TOP 1 [Id] FROM [Work].[WorkTypeHierarchies]), 
                '{timestamp}', 
                '{timestamp}', 
                '{Guid.Empty}', 
                '{Guid.Empty}')");

        migrationBuilder.Sql($@"
            UPDATE [Work].[WorkTypes]
            SET [LevelId] = (SELECT [Id] FROM [Work].[WorkTypeLevels] WHERE [Tier] = '{defaultTier}' AND [Ownership] = {ownershipId})
            WHERE [LevelId] IS NULL");

        migrationBuilder.AlterColumn<int>(
            name: "LevelId",
            schema: "Work",
            table: "WorkTypes",
            type: "int",
            nullable: false,
            defaultValue: 0,
            oldClrType: typeof(int),
            oldType: "int",
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AlterColumn<int>(
            name: "LevelId",
            schema: "Work",
            table: "WorkTypes",
            type: "int",
            nullable: true,
            oldClrType: typeof(int),
            oldType: "int");
    }
}
