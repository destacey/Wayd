using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class NormalizeAuditTrailValueObjects : Migration
{
    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // 1. Create a temporary filtered index on TableName to make seeks instantaneous
        // and avoid full-table scans across hundreds of thousands of unrelated audit rows.
        migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes 
                    WHERE name = 'IX_AuditTrails_Temp_NormalizeValueObjects' 
                      AND object_id = OBJECT_ID('[Auditing].[AuditTrails]')
                )
                BEGIN
                    CREATE NONCLUSTERED INDEX [IX_AuditTrails_Temp_NormalizeValueObjects]
                    ON [Auditing].[AuditTrails] ([TableName], [Id])
                    WHERE [TableName] IN ('Employee', 'EmployeeEmail', 'Project', 'ProjectPhase', 'ProjectStage', 'ProjectTask', 'Team', 'TeamOfTeams', 'WorkItem', 'Workspace');
                END
            ", suppressTransaction: true);

        // 2. Normalize Team & TeamOfTeams: code / Code -> $.code
        migrationBuilder.Sql(@"
                SET NOCOUNT ON;
                DECLARE @BatchSize int = 2500;
                DECLARE @Updated int = 1;

                WHILE @Updated > 0
                BEGIN
                    UPDATE TOP (@BatchSize) t
                    SET [NewValues] = JSON_MODIFY(
                            JSON_MODIFY(
                                t.[NewValues],
                                '$.code',
                                COALESCE(
                                    JSON_VALUE(t.[NewValues], '$.code.value'),
                                    JSON_VALUE(t.[NewValues], '$.code.Value'),
                                    JSON_VALUE(t.[NewValues], '$.Code.value'),
                                    JSON_VALUE(t.[NewValues], '$.Code.Value')
                                )
                            ),
                            '$.Code',
                            NULL
                        )
                    FROM [Auditing].[AuditTrails] t
                    WHERE t.[TableName] IN ('Team', 'TeamOfTeams')
                      AND (
                           JSON_VALUE(t.[NewValues], '$.code.value') IS NOT NULL
                        OR JSON_VALUE(t.[NewValues], '$.code.Value') IS NOT NULL
                        OR JSON_VALUE(t.[NewValues], '$.Code.value') IS NOT NULL
                        OR JSON_VALUE(t.[NewValues], '$.Code.Value') IS NOT NULL
                      );
                    SET @Updated = @@ROWCOUNT;
                END

                SET @Updated = 1;
                WHILE @Updated > 0
                BEGIN
                    UPDATE TOP (@BatchSize) t
                    SET [OldValues] = JSON_MODIFY(
                            JSON_MODIFY(
                                t.[OldValues],
                                '$.code',
                                COALESCE(
                                    JSON_VALUE(t.[OldValues], '$.code.value'),
                                    JSON_VALUE(t.[OldValues], '$.code.Value'),
                                    JSON_VALUE(t.[OldValues], '$.Code.value'),
                                    JSON_VALUE(t.[OldValues], '$.Code.Value')
                                )
                            ),
                            '$.Code',
                            NULL
                        )
                    FROM [Auditing].[AuditTrails] t
                    WHERE t.[TableName] IN ('Team', 'TeamOfTeams')
                      AND (
                           JSON_VALUE(t.[OldValues], '$.code.value') IS NOT NULL
                        OR JSON_VALUE(t.[OldValues], '$.code.Value') IS NOT NULL
                        OR JSON_VALUE(t.[OldValues], '$.Code.value') IS NOT NULL
                        OR JSON_VALUE(t.[OldValues], '$.Code.Value') IS NOT NULL
                      );
                    SET @Updated = @@ROWCOUNT;
                END
            ", suppressTransaction: true);

        // 3. Normalize Project & Workspace & WorkItem: key / Key -> $.key
        migrationBuilder.Sql(@"
                SET NOCOUNT ON;
                DECLARE @BatchSize int = 2500;
                DECLARE @Updated int = 1;

                WHILE @Updated > 0
                BEGIN
                    UPDATE TOP (@BatchSize) t
                    SET [NewValues] = JSON_MODIFY(
                            JSON_MODIFY(
                                t.[NewValues],
                                '$.key',
                                COALESCE(
                                    JSON_VALUE(t.[NewValues], '$.key.value'),
                                    JSON_VALUE(t.[NewValues], '$.key.Value'),
                                    JSON_VALUE(t.[NewValues], '$.Key.value'),
                                    JSON_VALUE(t.[NewValues], '$.Key.Value')
                                )
                            ),
                            '$.Key',
                            NULL
                        )
                    FROM [Auditing].[AuditTrails] t
                    WHERE t.[TableName] IN ('Project', 'Workspace', 'WorkItem')
                      AND (
                           JSON_VALUE(t.[NewValues], '$.key.value') IS NOT NULL
                        OR JSON_VALUE(t.[NewValues], '$.key.Value') IS NOT NULL
                        OR JSON_VALUE(t.[NewValues], '$.Key.value') IS NOT NULL
                        OR JSON_VALUE(t.[NewValues], '$.Key.Value') IS NOT NULL
                      );
                    SET @Updated = @@ROWCOUNT;
                END

                SET @Updated = 1;
                WHILE @Updated > 0
                BEGIN
                    UPDATE TOP (@BatchSize) t
                    SET [OldValues] = JSON_MODIFY(
                            JSON_MODIFY(
                                t.[OldValues],
                                '$.key',
                                COALESCE(
                                    JSON_VALUE(t.[OldValues], '$.key.value'),
                                    JSON_VALUE(t.[OldValues], '$.key.Value'),
                                    JSON_VALUE(t.[OldValues], '$.Key.value'),
                                    JSON_VALUE(t.[OldValues], '$.Key.Value')
                                )
                            ),
                            '$.Key',
                            NULL
                        )
                    FROM [Auditing].[AuditTrails] t
                    WHERE t.[TableName] IN ('Project', 'Workspace', 'WorkItem')
                      AND (
                           JSON_VALUE(t.[OldValues], '$.key.value') IS NOT NULL
                        OR JSON_VALUE(t.[OldValues], '$.key.Value') IS NOT NULL
                        OR JSON_VALUE(t.[OldValues], '$.Key.value') IS NOT NULL
                        OR JSON_VALUE(t.[OldValues], '$.Key.Value') IS NOT NULL
                      );
                    SET @Updated = @@ROWCOUNT;
                END
            ", suppressTransaction: true);

        // 4. Normalize ProjectTask: TaskKey -> $.taskKey, Key -> $.key (where object)
        migrationBuilder.Sql(@"
                SET NOCOUNT ON;
                DECLARE @BatchSize int = 2500;
                DECLARE @Updated int = 1;

                WHILE @Updated > 0
                BEGIN
                    UPDATE TOP (@BatchSize) t
                    SET [NewValues] = JSON_MODIFY(
                            JSON_MODIFY(
                                t.[NewValues],
                                '$.taskKey',
                                COALESCE(
                                    JSON_VALUE(t.[NewValues], '$.taskKey.value'),
                                    JSON_VALUE(t.[NewValues], '$.taskKey.Value'),
                                    JSON_VALUE(t.[NewValues], '$.TaskKey.value'),
                                    JSON_VALUE(t.[NewValues], '$.TaskKey.Value')
                                )
                            ),
                            '$.TaskKey',
                            NULL
                        )
                    FROM [Auditing].[AuditTrails] t
                    WHERE t.[TableName] = 'ProjectTask'
                      AND (
                           JSON_VALUE(t.[NewValues], '$.taskKey.value') IS NOT NULL
                        OR JSON_VALUE(t.[NewValues], '$.taskKey.Value') IS NOT NULL
                        OR JSON_VALUE(t.[NewValues], '$.TaskKey.value') IS NOT NULL
                        OR JSON_VALUE(t.[NewValues], '$.TaskKey.Value') IS NOT NULL
                      );
                    SET @Updated = @@ROWCOUNT;
                END

                SET @Updated = 1;
                WHILE @Updated > 0
                BEGIN
                    UPDATE TOP (@BatchSize) t
                    SET [OldValues] = JSON_MODIFY(
                            JSON_MODIFY(
                                t.[OldValues],
                                '$.taskKey',
                                COALESCE(
                                    JSON_VALUE(t.[OldValues], '$.taskKey.value'),
                                    JSON_VALUE(t.[OldValues], '$.taskKey.Value'),
                                    JSON_VALUE(t.[OldValues], '$.TaskKey.value'),
                                    JSON_VALUE(t.[OldValues], '$.TaskKey.Value')
                                )
                            ),
                            '$.TaskKey',
                            NULL
                        )
                    FROM [Auditing].[AuditTrails] t
                    WHERE t.[TableName] = 'ProjectTask'
                      AND (
                           JSON_VALUE(t.[OldValues], '$.taskKey.value') IS NOT NULL
                        OR JSON_VALUE(t.[OldValues], '$.taskKey.Value') IS NOT NULL
                        OR JSON_VALUE(t.[OldValues], '$.TaskKey.value') IS NOT NULL
                        OR JSON_VALUE(t.[OldValues], '$.TaskKey.Value') IS NOT NULL
                      );
                    SET @Updated = @@ROWCOUNT;
                END

                -- Also normalize Key where it holds a ProjectTaskKey value object
                SET @Updated = 1;
                WHILE @Updated > 0
                BEGIN
                    UPDATE TOP (@BatchSize) t
                    SET [NewValues] = JSON_MODIFY(
                            JSON_MODIFY(
                                t.[NewValues],
                                '$.key',
                                COALESCE(
                                    JSON_VALUE(t.[NewValues], '$.key.value'),
                                    JSON_VALUE(t.[NewValues], '$.key.Value'),
                                    JSON_VALUE(t.[NewValues], '$.Key.value'),
                                    JSON_VALUE(t.[NewValues], '$.Key.Value')
                                )
                            ),
                            '$.Key',
                            NULL
                        )
                    FROM [Auditing].[AuditTrails] t
                    WHERE t.[TableName] = 'ProjectTask'
                      AND (
                           JSON_VALUE(t.[NewValues], '$.key.value') IS NOT NULL
                        OR JSON_VALUE(t.[NewValues], '$.key.Value') IS NOT NULL
                        OR JSON_VALUE(t.[NewValues], '$.Key.value') IS NOT NULL
                        OR JSON_VALUE(t.[NewValues], '$.Key.Value') IS NOT NULL
                      );
                    SET @Updated = @@ROWCOUNT;
                END

                SET @Updated = 1;
                WHILE @Updated > 0
                BEGIN
                    UPDATE TOP (@BatchSize) t
                    SET [OldValues] = JSON_MODIFY(
                            JSON_MODIFY(
                                t.[OldValues],
                                '$.key',
                                COALESCE(
                                    JSON_VALUE(t.[OldValues], '$.key.value'),
                                    JSON_VALUE(t.[OldValues], '$.key.Value'),
                                    JSON_VALUE(t.[OldValues], '$.Key.value'),
                                    JSON_VALUE(t.[OldValues], '$.Key.Value')
                                )
                            ),
                            '$.Key',
                            NULL
                        )
                    FROM [Auditing].[AuditTrails] t
                    WHERE t.[TableName] = 'ProjectTask'
                      AND (
                           JSON_VALUE(t.[OldValues], '$.key.value') IS NOT NULL
                        OR JSON_VALUE(t.[OldValues], '$.key.Value') IS NOT NULL
                        OR JSON_VALUE(t.[OldValues], '$.Key.value') IS NOT NULL
                        OR JSON_VALUE(t.[OldValues], '$.Key.Value') IS NOT NULL
                      );
                    SET @Updated = @@ROWCOUNT;
                END
            ", suppressTransaction: true);

        // 5. Normalize Progress on ProjectTask, ProjectStage, ProjectPhase: Progress -> $.progress (numeric)
        migrationBuilder.Sql(@"
                SET NOCOUNT ON;
                DECLARE @BatchSize int = 2500;
                DECLARE @Updated int = 1;

                WHILE @Updated > 0
                BEGIN
                    UPDATE TOP (@BatchSize) t
                    SET [NewValues] = JSON_MODIFY(
                            JSON_MODIFY(
                                t.[NewValues],
                                '$.progress',
                                CAST(COALESCE(
                                    JSON_VALUE(t.[NewValues], '$.progress.value'),
                                    JSON_VALUE(t.[NewValues], '$.progress.Value'),
                                    JSON_VALUE(t.[NewValues], '$.Progress.value'),
                                    JSON_VALUE(t.[NewValues], '$.Progress.Value')
                                ) AS decimal(5,2))
                            ),
                            '$.Progress',
                            NULL
                        )
                    FROM [Auditing].[AuditTrails] t
                    WHERE t.[TableName] IN ('ProjectTask', 'ProjectStage', 'ProjectPhase')
                      AND (
                           JSON_VALUE(t.[NewValues], '$.progress.value') IS NOT NULL
                        OR JSON_VALUE(t.[NewValues], '$.progress.Value') IS NOT NULL
                        OR JSON_VALUE(t.[NewValues], '$.Progress.value') IS NOT NULL
                        OR JSON_VALUE(t.[NewValues], '$.Progress.Value') IS NOT NULL
                      );
                    SET @Updated = @@ROWCOUNT;
                END

                SET @Updated = 1;
                WHILE @Updated > 0
                BEGIN
                    UPDATE TOP (@BatchSize) t
                    SET [OldValues] = JSON_MODIFY(
                            JSON_MODIFY(
                                t.[OldValues],
                                '$.progress',
                                CAST(COALESCE(
                                    JSON_VALUE(t.[OldValues], '$.progress.value'),
                                    JSON_VALUE(t.[OldValues], '$.progress.Value'),
                                    JSON_VALUE(t.[OldValues], '$.Progress.value'),
                                    JSON_VALUE(t.[OldValues], '$.Progress.Value')
                                ) AS decimal(5,2))
                            ),
                            '$.Progress',
                            NULL
                        )
                    FROM [Auditing].[AuditTrails] t
                    WHERE t.[TableName] IN ('ProjectTask', 'ProjectStage', 'ProjectPhase')
                      AND (
                           JSON_VALUE(t.[OldValues], '$.progress.value') IS NOT NULL
                        OR JSON_VALUE(t.[OldValues], '$.progress.Value') IS NOT NULL
                        OR JSON_VALUE(t.[OldValues], '$.Progress.value') IS NOT NULL
                        OR JSON_VALUE(t.[OldValues], '$.Progress.Value') IS NOT NULL
                      );
                    SET @Updated = @@ROWCOUNT;
                END
            ", suppressTransaction: true);

        // 6. Normalize Employee & EmployeeEmail: email / Email -> $.email
        migrationBuilder.Sql(@"
                SET NOCOUNT ON;
                DECLARE @BatchSize int = 2500;
                DECLARE @Updated int = 1;

                WHILE @Updated > 0
                BEGIN
                    UPDATE TOP (@BatchSize) t
                    SET [NewValues] = JSON_MODIFY(
                            JSON_MODIFY(
                                t.[NewValues],
                                '$.email',
                                COALESCE(
                                    JSON_VALUE(t.[NewValues], '$.email.value'),
                                    JSON_VALUE(t.[NewValues], '$.email.Value'),
                                    JSON_VALUE(t.[NewValues], '$.Email.value'),
                                    JSON_VALUE(t.[NewValues], '$.Email.Value')
                                )
                            ),
                            '$.Email',
                            NULL
                        )
                    FROM [Auditing].[AuditTrails] t
                    WHERE t.[TableName] IN ('Employee', 'EmployeeEmail')
                      AND (
                           JSON_VALUE(t.[NewValues], '$.email.value') IS NOT NULL
                        OR JSON_VALUE(t.[NewValues], '$.email.Value') IS NOT NULL
                        OR JSON_VALUE(t.[NewValues], '$.Email.value') IS NOT NULL
                        OR JSON_VALUE(t.[NewValues], '$.Email.Value') IS NOT NULL
                      );
                    SET @Updated = @@ROWCOUNT;
                END

                SET @Updated = 1;
                WHILE @Updated > 0
                BEGIN
                    UPDATE TOP (@BatchSize) t
                    SET [OldValues] = JSON_MODIFY(
                            JSON_MODIFY(
                                t.[OldValues],
                                '$.email',
                                COALESCE(
                                    JSON_VALUE(t.[OldValues], '$.email.value'),
                                    JSON_VALUE(t.[OldValues], '$.email.Value'),
                                    JSON_VALUE(t.[OldValues], '$.Email.value'),
                                    JSON_VALUE(t.[OldValues], '$.Email.Value')
                                )
                            ),
                            '$.Email',
                            NULL
                        )
                    FROM [Auditing].[AuditTrails] t
                    WHERE t.[TableName] IN ('Employee', 'EmployeeEmail')
                      AND (
                           JSON_VALUE(t.[OldValues], '$.email.value') IS NOT NULL
                        OR JSON_VALUE(t.[OldValues], '$.email.Value') IS NOT NULL
                        OR JSON_VALUE(t.[OldValues], '$.Email.value') IS NOT NULL
                        OR JSON_VALUE(t.[OldValues], '$.Email.Value') IS NOT NULL
                      );
                    SET @Updated = @@ROWCOUNT;
                END
            ", suppressTransaction: true);

        // 7. Drop the temporary filtered index
        migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.indexes 
                    WHERE name = 'IX_AuditTrails_Temp_NormalizeValueObjects' 
                      AND object_id = OBJECT_ID('[Auditing].[AuditTrails]')
                )
                BEGIN
                    DROP INDEX [IX_AuditTrails_Temp_NormalizeValueObjects] ON [Auditing].[AuditTrails];
                END
            ", suppressTransaction: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
    }
}
