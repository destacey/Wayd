using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class SeedExternalIdentityMappings : Migration
{
    // Seeds AppIntegrations.ExternalIdentityMappings from the attributions Azure DevOps syncs have
    // already resolved, so the People tab opens with a full picture rather than filling in one sync
    // at a time.
    //
    // Scope note, and it is a real limit: work items store only the resolved EmployeeId. The Azure
    // DevOps identity GUID was discarded at deserialization until this release, so it exists nowhere
    // in the database to seed from. Rows are therefore keyed on the employee's canonical address —
    // what the old matching resolved on — and marked AutoMatched. The first sync after this migration
    // re-keys each row on the real identity GUID.
    //
    // The consequence: only people who ALREADY resolved can be seeded. The unmapped ones — the reason
    // this feature exists — cannot be recovered here, because nothing was ever recorded about them.
    // They appear after the first sync.

    private const string SystemUserId = "11111111-1111-1111-1111-111111111111"; // SystemIdentity.UserId
    private const string CorrelationId = "8f47c2d1-3b95-4e08-a6f2-9d1e5c703a84"; // lets Down remove exactly the rows this migration added.

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql($@"
                    DECLARE @Now datetime2(7) = SYSUTCDATETIME();

                    DECLARE @Seeded TABLE
                    (
                        Id uniqueidentifier NOT NULL,
                        ConnectionId uniqueidentifier NOT NULL,
                        ExternalId nvarchar(128) NOT NULL,
                        Email nvarchar(256) NULL,
                        DisplayName nvarchar(256) NULL,
                        EmployeeId uniqueidentifier NOT NULL,
                        LastSeen datetime2(7) NOT NULL
                    );

                    -- One row per (connection, employee) that Azure DevOps work has been attributed to.
                    -- Connections join to work items through Workspaces.SystemId, the Azure DevOps
                    -- organization id both sides already carry.
                    --
                    -- Every attribution column counts: someone who only ever created or last-modified an
                    -- item is still an identity an admin may need to remap, not just current assignees.
                    WITH [Attributions] AS
                    (
                        SELECT wi.[WorkspaceId], wi.[AssignedToId] AS [EmployeeId], wi.[LastModified]
                        FROM [Work].[WorkItems] wi
                        WHERE wi.[AssignedToId] IS NOT NULL
                        UNION ALL
                        SELECT wi.[WorkspaceId], wi.[CreatedById], wi.[LastModified]
                        FROM [Work].[WorkItems] wi
                        WHERE wi.[CreatedById] IS NOT NULL
                        UNION ALL
                        SELECT wi.[WorkspaceId], wi.[LastModifiedById], wi.[LastModified]
                        FROM [Work].[WorkItems] wi
                        WHERE wi.[LastModifiedById] IS NOT NULL
                    ),
                    [Resolved] AS
                    (
                        SELECT
                            c.[Id] AS [ConnectionId],
                            a.[EmployeeId],
                            MAX(a.[LastModified]) AS [LastSeen]
                        FROM [Attributions] a
                        INNER JOIN [Work].[Workspaces] w
                            ON w.[Id] = a.[WorkspaceId]
                           AND w.[SystemId] IS NOT NULL
                        INNER JOIN [AppIntegrations].[Connections] c
                            ON c.[SystemId] = w.[SystemId]
                           AND c.[Connector] = 'AzureDevOps'
                           AND c.[IsDeleted] = 0
                        GROUP BY c.[Id], a.[EmployeeId]
                    )
                    INSERT INTO [AppIntegrations].[ExternalIdentityMappings]
                        ([Id], [Connector], [ConnectionId], [ExternalId], [Email], [DisplayName], [Handle],
                         [EmployeeId], [Status], [LastSeen],
                         [SystemCreated], [SystemCreatedBy], [SystemLastModified], [SystemLastModifiedBy])
                    OUTPUT inserted.[Id], inserted.[ConnectionId], inserted.[ExternalId], inserted.[Email],
                           inserted.[DisplayName], inserted.[EmployeeId], inserted.[LastSeen] INTO @Seeded
                    SELECT
                        NEWID(),
                        'AzureDevOps',
                        r.[ConnectionId],
                        -- Placeholder key: the real Azure DevOps identity GUID was never stored. The first
                        -- sync after this migration matches this row by address and re-keys it.
                        e.[Email],
                        e.[Email],
                        LTRIM(RTRIM(e.[FirstName] + ' ' + e.[LastName])),
                        e.[Email],
                        r.[EmployeeId],
                        'AutoMatched',
                        r.[LastSeen],
                        @Now,
                        '{SystemUserId}',
                        @Now,
                        '{SystemUserId}'
                    FROM [Resolved] r
                    INNER JOIN [Organization].[Employees] e
                        ON e.[Id] = r.[EmployeeId]
                       AND e.[IsDeleted] = 0
                    -- Guards the unique index on (ConnectionId, ExternalId) and makes a re-run a no-op.
                    WHERE NOT EXISTS
                    (
                        SELECT 1
                        FROM [AppIntegrations].[ExternalIdentityMappings] m
                        WHERE m.[ConnectionId] = r.[ConnectionId]
                          AND m.[ExternalId] = e.[Email]
                    );

                    INSERT INTO [Auditing].[AuditTrails]
                        ([Id], [UserId], [Type], [SchemaName], [TableName], [DateTime], [OldValues], [NewValues], [AffectedColumns], [PrimaryKey], [CorrelationId])
                    SELECT
                        NEWID(),
                        '{SystemUserId}',
                        'Create',
                        'AppIntegrations',
                        'ExternalIdentityMapping',
                        @Now,
                        NULL,
                        '{{""Connector"":""AzureDevOps""' +
                            ',""ConnectionId"":""' + LOWER(CONVERT(varchar(36), s.[ConnectionId])) + '""' +
                            ',""ExternalId"":""' + STRING_ESCAPE(s.[ExternalId], 'json') + '""' +
                            ',""Email"":""' + STRING_ESCAPE(s.[Email], 'json') + '""' +
                            ',""DisplayName"":""' + STRING_ESCAPE(s.[DisplayName], 'json') + '""' +
                            ',""EmployeeId"":""' + LOWER(CONVERT(varchar(36), s.[EmployeeId])) + '""' +
                            ',""Status"":""AutoMatched""' +
                            ',""LastSeen"":""' + CONVERT(varchar(33), s.[LastSeen], 127) + '""' +
                            ',""SystemCreated"":""' + CONVERT(varchar(33), @Now, 127) + '""' +
                            ',""SystemCreatedBy"":""{SystemUserId}""' +
                            ',""SystemLastModified"":""' + CONVERT(varchar(33), @Now, 127) + '""' +
                            ',""SystemLastModifiedBy"":""{SystemUserId}""}}',
                        -- NULL, matching what the app writes: the auditing interceptor only populates
                        -- AffectedColumns for Modified entries, never for Added ones.
                        NULL,
                        '{{""Id"":""' + LOWER(CONVERT(varchar(36), s.[Id])) + '""}}',
                        '{CorrelationId}'
                    FROM @Seeded s;
                ");

    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {

        // Removes exactly the rows this migration created, identified through its own audit trail.
        //
        // Skipped entirely if any seeded row has since been touched — a sync that re-keyed the
        // placeholder, or an admin who remapped or ignored it. Removing those would discard a
        // decision made after this ran.
        migrationBuilder.Sql($@"
                    IF NOT EXISTS (
                        SELECT 1
                        FROM [Auditing].[AuditTrails] mine
                        WHERE mine.[CorrelationId] = '{CorrelationId}'
                          AND EXISTS (
                                SELECT 1
                                FROM [Auditing].[AuditTrails] later
                                WHERE later.[CorrelationId] <> '{CorrelationId}'
                                  AND later.[SchemaName] = 'AppIntegrations'
                                  AND later.[TableName] = 'ExternalIdentityMapping'
                                  AND later.[PrimaryKey] = mine.[PrimaryKey]
                                  AND later.[DateTime] > mine.[DateTime]
                          )
                    )
                    BEGIN
                        DELETE m
                        FROM [AppIntegrations].[ExternalIdentityMappings] m
                        INNER JOIN [Auditing].[AuditTrails] a
                            ON a.[PrimaryKey] = '{{""Id"":""' + LOWER(CONVERT(varchar(36), m.[Id])) + '""}}'
                        WHERE a.[CorrelationId] = '{CorrelationId}'
                          AND a.[SchemaName] = 'AppIntegrations'
                          AND a.[TableName] = 'ExternalIdentityMapping'
                          AND a.[Type] = 'Create';

                        DELETE FROM [Auditing].[AuditTrails]
                        WHERE [CorrelationId] = '{CorrelationId}'
                          AND [SchemaName] = 'AppIntegrations'
                          AND [TableName] = 'ExternalIdentityMapping'
                          AND [Type] = 'Create';
                    END
                ");
    }
}
