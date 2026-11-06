using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <summary>
/// Brings stored incident workflows up to the seeded lifecycle. A system workflow cannot be edited
/// by the seeder, so the change is SQL: fold a draft named "Default Incident Workflow v2" into the
/// system workflow, then give that workflow and the organization-assigned workflow — a different row
/// when the assignment does not point at the system workflow — Responding, Closed, Recovered, and
/// the restricted transitions.
/// </summary>
public partial class UpgradeIncidentWorkflowLifecycle : Migration
{
    // ProductStatusAlias: Open=30, Resolved=31, Canceled=32, Responding=33, Closed=34.
    // Idempotent: safe when the seeder already created the full lifecycle on a fresh database.
    // Two batches so a RETURN in the system-workflow step cannot skip the assigned workflow.

    public const string UpSql = """
        DECLARE @OwnerType varchar(64) = 'product.incident';
        DECLARE @SystemName nvarchar(128) = N'Default Incident Workflow';
        DECLARE @V2Name nvarchar(128) = N'Default Incident Workflow v2';
        DECLARE @Now datetime2 = SYSUTCDATETIME();

        DECLARE @SystemWorkflowId uniqueidentifier;
        SELECT @SystemWorkflowId = [Id]
        FROM [StatusWorkflows].[StatusWorkflows]
        WHERE [OwnerType] = @OwnerType AND [IsSystem] = 1 AND [Name] = @SystemName;

        IF @SystemWorkflowId IS NULL
            RETURN;

        DECLARE @V2WorkflowId uniqueidentifier;
        SELECT @V2WorkflowId = [Id]
        FROM [StatusWorkflows].[StatusWorkflows]
        WHERE [OwnerType] = @OwnerType AND [Name] = @V2Name AND [Id] <> @SystemWorkflowId;

        IF @V2WorkflowId IS NOT NULL
        BEGIN
            UPDATE i
            SET
                i.[StatusWorkflowId] = @SystemWorkflowId,
                i.[StatusId] = tgt.[Id],
                i.[StatusName] = tgt.[Name],
                i.[StatusCategory] = tgt.[Category],
                i.[StatusAliasValue] = tgt.[Alias],
                i.[SystemLastModified] = @Now
            FROM [ProductManagement].[Incidents] i
            INNER JOIN [StatusWorkflows].[WorkflowStatuses] src ON src.[Id] = i.[StatusId] AND src.[WorkflowId] = @V2WorkflowId
            INNER JOIN [StatusWorkflows].[WorkflowStatuses] tgt ON tgt.[WorkflowId] = @SystemWorkflowId AND tgt.[Alias] = src.[Alias]
            WHERE i.[StatusWorkflowId] = @V2WorkflowId;

            UPDATE st
            SET st.[WorkflowId] = @SystemWorkflowId
            FROM [StatusWorkflows].[StatusTransitions] st
            WHERE st.[OwnerType] = @OwnerType AND st.[WorkflowId] = @V2WorkflowId;

            UPDATE [StatusWorkflows].[WorkflowAssignments]
            SET [WorkflowId] = @SystemWorkflowId, [SystemLastModified] = @Now
            WHERE [OwnerType] = @OwnerType AND [ScopeId] IS NULL AND [WorkflowId] = @V2WorkflowId;

            DELETE FROM [StatusWorkflows].[StatusWorkflows] WHERE [Id] = @V2WorkflowId;
        END;

        DECLARE @NextOrder int = ISNULL((SELECT MAX([Order]) FROM [StatusWorkflows].[WorkflowStatuses] WHERE [WorkflowId] = @SystemWorkflowId), 0);

        IF NOT EXISTS (SELECT 1 FROM [StatusWorkflows].[WorkflowStatuses] WHERE [WorkflowId] = @SystemWorkflowId AND [Alias] = 33)
        BEGIN
            SET @NextOrder = @NextOrder + 1;
            INSERT INTO [StatusWorkflows].[WorkflowStatuses] (
                [Id], [WorkflowId], [Name], [Description], [Category], [Alias], [Order], [IsSystem],
                [SystemCreated], [SystemLastModified])
            VALUES (
                NEWID(), @SystemWorkflowId, N'Responding', N'Active response is under way.', 'Active', 33, @NextOrder, 1,
                @Now, @Now);
        END;

        IF NOT EXISTS (SELECT 1 FROM [StatusWorkflows].[WorkflowStatuses] WHERE [WorkflowId] = @SystemWorkflowId AND [Alias] = 34)
        BEGIN
            SET @NextOrder = (SELECT MAX([Order]) FROM [StatusWorkflows].[WorkflowStatuses] WHERE [WorkflowId] = @SystemWorkflowId);
            SET @NextOrder = @NextOrder + 1;
            INSERT INTO [StatusWorkflows].[WorkflowStatuses] (
                [Id], [WorkflowId], [Name], [Description], [Category], [Alias], [Order], [IsSystem],
                [SystemCreated], [SystemLastModified])
            VALUES (
                NEWID(), @SystemWorkflowId, N'Closed', N'Closed after recovery.', 'Done', 34, @NextOrder, 1,
                @Now, @Now);
        END;

        UPDATE [StatusWorkflows].[WorkflowStatuses]
        SET [Name] = N'Recovered', [Description] = N'Service restored.', [SystemLastModified] = @Now
        WHERE [WorkflowId] = @SystemWorkflowId AND [Alias] = 31 AND [Name] <> N'Recovered';

        UPDATE i
        SET
            i.[StatusId] = ws.[Id],
            i.[StatusName] = ws.[Name],
            i.[StatusCategory] = ws.[Category],
            i.[StatusAliasValue] = ws.[Alias],
            i.[SystemLastModified] = @Now
        FROM [ProductManagement].[Incidents] i
        INNER JOIN [StatusWorkflows].[WorkflowStatuses] ws ON ws.[WorkflowId] = @SystemWorkflowId AND ws.[Alias] = i.[StatusAliasValue]
        WHERE i.[StatusWorkflowId] = @SystemWorkflowId
          AND (i.[StatusId] <> ws.[Id] OR i.[StatusName] <> ws.[Name] OR i.[StatusCategory] <> ws.[Category]);

        DECLARE @OpenId uniqueidentifier;
        DECLARE @ResolvedId uniqueidentifier;
        DECLARE @CanceledId uniqueidentifier;
        DECLARE @RespondingId uniqueidentifier;
        DECLARE @ClosedId uniqueidentifier;

        SELECT @OpenId = [Id] FROM [StatusWorkflows].[WorkflowStatuses] WHERE [WorkflowId] = @SystemWorkflowId AND [Alias] = 30;
        SELECT @ResolvedId = [Id] FROM [StatusWorkflows].[WorkflowStatuses] WHERE [WorkflowId] = @SystemWorkflowId AND [Alias] = 31;
        SELECT @CanceledId = [Id] FROM [StatusWorkflows].[WorkflowStatuses] WHERE [WorkflowId] = @SystemWorkflowId AND [Alias] = 32;
        SELECT @RespondingId = [Id] FROM [StatusWorkflows].[WorkflowStatuses] WHERE [WorkflowId] = @SystemWorkflowId AND [Alias] = 33;
        SELECT @ClosedId = [Id] FROM [StatusWorkflows].[WorkflowStatuses] WHERE [WorkflowId] = @SystemWorkflowId AND [Alias] = 34;

        IF @OpenId IS NULL OR @ResolvedId IS NULL OR @CanceledId IS NULL OR @RespondingId IS NULL OR @ClosedId IS NULL
            RETURN;

        IF NOT EXISTS (
            SELECT 1
            FROM [StatusWorkflows].[StatusWorkflows] w
            WHERE w.[Id] = @SystemWorkflowId
              AND w.[TransitionMode] = 'Restricted'
              AND (SELECT COUNT(*) FROM [StatusWorkflows].[WorkflowTransitions] t WHERE t.[WorkflowId] = @SystemWorkflowId) = 5
              AND EXISTS (SELECT 1 FROM [StatusWorkflows].[WorkflowTransitions] t WHERE t.[WorkflowId] = @SystemWorkflowId AND t.[FromStatusId] = @OpenId AND t.[ToStatusId] = @RespondingId)
              AND EXISTS (SELECT 1 FROM [StatusWorkflows].[WorkflowTransitions] t WHERE t.[WorkflowId] = @SystemWorkflowId AND t.[FromStatusId] = @RespondingId AND t.[ToStatusId] = @ResolvedId)
              AND EXISTS (SELECT 1 FROM [StatusWorkflows].[WorkflowTransitions] t WHERE t.[WorkflowId] = @SystemWorkflowId AND t.[FromStatusId] = @ResolvedId AND t.[ToStatusId] = @ClosedId)
              AND EXISTS (SELECT 1 FROM [StatusWorkflows].[WorkflowTransitions] t WHERE t.[WorkflowId] = @SystemWorkflowId AND t.[FromStatusId] = @OpenId AND t.[ToStatusId] = @CanceledId)
              AND EXISTS (SELECT 1 FROM [StatusWorkflows].[WorkflowTransitions] t WHERE t.[WorkflowId] = @SystemWorkflowId AND t.[FromStatusId] = @RespondingId AND t.[ToStatusId] = @CanceledId))
        BEGIN
            UPDATE [StatusWorkflows].[StatusWorkflows]
            SET [TransitionMode] = 'Restricted', [SystemLastModified] = @Now
            WHERE [Id] = @SystemWorkflowId;

            DELETE FROM [StatusWorkflows].[WorkflowTransitions] WHERE [WorkflowId] = @SystemWorkflowId;

            INSERT INTO [StatusWorkflows].[WorkflowTransitions] (
                [Id], [WorkflowId], [FromStatusId], [ToStatusId], [SystemCreated], [SystemLastModified])
            VALUES
                (NEWID(), @SystemWorkflowId, @OpenId, @RespondingId, @Now, @Now),
                (NEWID(), @SystemWorkflowId, @RespondingId, @ResolvedId, @Now, @Now),
                (NEWID(), @SystemWorkflowId, @ResolvedId, @ClosedId, @Now, @Now),
                (NEWID(), @SystemWorkflowId, @OpenId, @CanceledId, @Now, @Now),
                (NEWID(), @SystemWorkflowId, @RespondingId, @CanceledId, @Now, @Now);
        END;
        """;

    /// <summary>
    /// The organization-assigned incident workflow. It carries the same lifecycle as the system workflow,
    /// including when the assignment points at a different row.
    /// </summary>
    public const string AssignedWorkflowSql = """
        DECLARE @OwnerType varchar(64) = 'product.incident';
        DECLARE @Now datetime2 = SYSUTCDATETIME();

        DECLARE @WorkflowId uniqueidentifier;
        SELECT @WorkflowId = [WorkflowId]
        FROM [StatusWorkflows].[WorkflowAssignments]
        WHERE [OwnerType] = @OwnerType AND [ScopeId] IS NULL;

        IF @WorkflowId IS NULL
            RETURN;

        DECLARE @NextOrder int = ISNULL((SELECT MAX([Order]) FROM [StatusWorkflows].[WorkflowStatuses] WHERE [WorkflowId] = @WorkflowId), 0);

        IF NOT EXISTS (SELECT 1 FROM [StatusWorkflows].[WorkflowStatuses] WHERE [WorkflowId] = @WorkflowId AND [Alias] = 33)
        BEGIN
            SET @NextOrder = @NextOrder + 1;
            INSERT INTO [StatusWorkflows].[WorkflowStatuses] (
                [Id], [WorkflowId], [Name], [Description], [Category], [Alias], [Order], [IsSystem],
                [SystemCreated], [SystemLastModified])
            VALUES (
                NEWID(), @WorkflowId, N'Responding', N'Active response is under way.', 'Active', 33, @NextOrder, 1,
                @Now, @Now);
        END;

        IF NOT EXISTS (SELECT 1 FROM [StatusWorkflows].[WorkflowStatuses] WHERE [WorkflowId] = @WorkflowId AND [Alias] = 34)
        BEGIN
            SET @NextOrder = (SELECT MAX([Order]) FROM [StatusWorkflows].[WorkflowStatuses] WHERE [WorkflowId] = @WorkflowId);
            SET @NextOrder = @NextOrder + 1;
            INSERT INTO [StatusWorkflows].[WorkflowStatuses] (
                [Id], [WorkflowId], [Name], [Description], [Category], [Alias], [Order], [IsSystem],
                [SystemCreated], [SystemLastModified])
            VALUES (
                NEWID(), @WorkflowId, N'Closed', N'Closed after recovery.', 'Done', 34, @NextOrder, 1,
                @Now, @Now);
        END;

        UPDATE [StatusWorkflows].[WorkflowStatuses]
        SET [Name] = N'Recovered', [Description] = N'Service restored.', [SystemLastModified] = @Now
        WHERE [WorkflowId] = @WorkflowId AND [Alias] = 31 AND [Name] <> N'Recovered';

        UPDATE i
        SET
            i.[StatusId] = ws.[Id],
            i.[StatusName] = ws.[Name],
            i.[StatusCategory] = ws.[Category],
            i.[StatusAliasValue] = ws.[Alias],
            i.[StatusWorkflowId] = @WorkflowId,
            i.[SystemLastModified] = @Now
        FROM [ProductManagement].[Incidents] i
        INNER JOIN [StatusWorkflows].[WorkflowStatuses] ws ON ws.[WorkflowId] = @WorkflowId AND ws.[Alias] = i.[StatusAliasValue]
        WHERE i.[StatusWorkflowId] <> @WorkflowId
           OR i.[StatusId] <> ws.[Id]
           OR i.[StatusName] <> ws.[Name]
           OR i.[StatusCategory] <> ws.[Category];

        DECLARE @OpenId uniqueidentifier;
        DECLARE @ResolvedId uniqueidentifier;
        DECLARE @CanceledId uniqueidentifier;
        DECLARE @RespondingId uniqueidentifier;
        DECLARE @ClosedId uniqueidentifier;

        SELECT @OpenId = [Id] FROM [StatusWorkflows].[WorkflowStatuses] WHERE [WorkflowId] = @WorkflowId AND [Alias] = 30;
        SELECT @ResolvedId = [Id] FROM [StatusWorkflows].[WorkflowStatuses] WHERE [WorkflowId] = @WorkflowId AND [Alias] = 31;
        SELECT @CanceledId = [Id] FROM [StatusWorkflows].[WorkflowStatuses] WHERE [WorkflowId] = @WorkflowId AND [Alias] = 32;
        SELECT @RespondingId = [Id] FROM [StatusWorkflows].[WorkflowStatuses] WHERE [WorkflowId] = @WorkflowId AND [Alias] = 33;
        SELECT @ClosedId = [Id] FROM [StatusWorkflows].[WorkflowStatuses] WHERE [WorkflowId] = @WorkflowId AND [Alias] = 34;

        IF @OpenId IS NULL OR @ResolvedId IS NULL OR @CanceledId IS NULL OR @RespondingId IS NULL OR @ClosedId IS NULL
            RETURN;

        IF NOT EXISTS (
            SELECT 1 FROM [StatusWorkflows].[WorkflowTransitions] t
            WHERE t.[WorkflowId] = @WorkflowId AND t.[FromStatusId] = @RespondingId AND t.[ToStatusId] = @ResolvedId)
        BEGIN
            UPDATE [StatusWorkflows].[StatusWorkflows]
            SET [TransitionMode] = 'Restricted', [SystemLastModified] = @Now
            WHERE [Id] = @WorkflowId;

            DELETE FROM [StatusWorkflows].[WorkflowTransitions] WHERE [WorkflowId] = @WorkflowId;

            INSERT INTO [StatusWorkflows].[WorkflowTransitions] (
                [Id], [WorkflowId], [FromStatusId], [ToStatusId], [SystemCreated], [SystemLastModified])
            VALUES
                (NEWID(), @WorkflowId, @OpenId, @RespondingId, @Now, @Now),
                (NEWID(), @WorkflowId, @RespondingId, @ResolvedId, @Now, @Now),
                (NEWID(), @WorkflowId, @ResolvedId, @ClosedId, @Now, @Now),
                (NEWID(), @WorkflowId, @OpenId, @CanceledId, @Now, @Now),
                (NEWID(), @WorkflowId, @RespondingId, @CanceledId, @Now, @Now);
        END;
        """;

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(UpSql);
        migrationBuilder.Sql(AssignedWorkflowSql);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
