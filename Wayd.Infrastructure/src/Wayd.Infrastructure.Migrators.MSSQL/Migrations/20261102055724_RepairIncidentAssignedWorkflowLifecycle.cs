using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
/// <summary>
/// Ensures the organization-assigned incident workflow carries the full lifecycle statuses and
/// Responding→Recovered/Cancel transitions. The earlier upgrade targeted the system workflow alone.
/// </summary>
public partial class RepairIncidentAssignedWorkflowLifecycle : Migration
{
    public const string UpSql = """
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
    protected override void Up(MigrationBuilder migrationBuilder) => migrationBuilder.Sql(UpSql);

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
