using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Wayd.Infrastructure.Migrators.MSSQL.Migrations;

/// <inheritdoc />
public partial class AddActivityLogCategory : Migration
{
    // Existing rows are categorised by EventType in two passes. The list below is what each event type declared
    // when this migration was written, frozen here because a migration must not read the domain: an old row gets
    // the badge a new row of the same type gets. A row whose type is no longer in the codebase (the Goals module's
    // events, for one) falls back to the name rules the Activity section used before the column existed, so it
    // keeps the badge it had.

    /// <inheritdoc />
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Category",
            schema: "App",
            table: "ActivityLogs",
            type: "varchar(32)",
            maxLength: 32,
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE a
            SET a.Category = c.Category
            FROM App.ActivityLogs a
            INNER JOIN (VALUES
            ('ApplicationRoleCreatedEvent', 'Created'),
            ('ApplicationUserCreatedEvent', 'Created'),
            ('DeploymentStartedEvent', 'Created'),
            ('EnvironmentAddedEvent', 'Created'),
            ('IterationCreatedEvent', 'Created'),
            ('PackageAssembledEvent', 'Created'),
            ('ProductAddedEvent', 'Created'),
            ('ProgramCreatedEvent', 'Created'),
            ('ProjectCreatedEvent', 'Created'),
            ('ProjectPortfolioCreatedEvent', 'Created'),
            ('ReleasePlannedEvent', 'Created'),
            ('StrategicInitiativeCreatedEvent', 'Created'),
            ('StrategicThemeCreatedEvent', 'Created'),
            ('TeamCreatedEvent', 'Created'),
            ('VersionPlannedEvent', 'Created'),
            ('ProjectHealthCheckAddedEvent', 'Health'),
            ('ProjectHealthCheckAddedEventV2', 'Health'),
            ('ProjectHealthCheckRemovedEvent', 'Health'),
            ('ProjectHealthCheckRemovedEventV2', 'Health'),
            ('ProjectHealthCheckUpdatedEvent', 'Health'),
            ('ProjectHealthCheckUpdatedEventV2', 'Health'),
            ('ApplicationRoleDeletedEvent', 'Removed'),
            ('IterationDeletedEvent', 'Removed'),
            ('ProductRemovedEvent', 'Removed'),
            ('ProgramDeletedEvent', 'Removed'),
            ('ProjectDeletedEvent', 'Removed'),
            ('StrategicInitiativeDeletedEvent', 'Removed'),
            ('StrategicThemeDeletedEvent', 'Removed'),
            ('TeamDeletedEvent', 'Removed'),
            ('IterationDateRangeChangedEvent', 'ScheduleChanged'),
            ('ProgramTimelineChangedEvent', 'ScheduleChanged'),
            ('ProjectTimelineChangedEvent', 'ScheduleChanged'),
            ('ProjectTimelineChangedEventV2', 'ScheduleChanged'),
            ('ReleaseDatesCorrectedEvent', 'ScheduleChanged'),
            ('ReleaseTargetDateMovedEvent', 'ScheduleChanged'),
            ('VersionDatesCorrectedEvent', 'ScheduleChanged'),
            ('VersionTargetDateMovedEvent', 'ScheduleChanged'),
            ('WorkIterationDateRangeChangedEvent', 'ScheduleChanged'),
            ('ApplicationUserActivatedEvent', 'StateChanged'),
            ('ApplicationUserDeactivatedEvent', 'StateChanged'),
            ('EnvironmentRetiredEvent', 'StateChanged'),
            ('EnvironmentRetiredEventV2', 'StateChanged'),
            ('IntegrationStateChangedEvent`1', 'StateChanged'),
            ('StrategicThemeActivatedEvent', 'StateChanged'),
            ('StrategicThemeArchivedEvent', 'StateChanged'),
            ('TeamActivatedEvent', 'StateChanged'),
            ('TeamDeactivatedEvent', 'StateChanged'),
            ('WorkflowArchivedEvent', 'StateChanged'),
            ('WorkflowArchivedEventV2', 'StateChanged'),
            ('WorkflowPublishedEvent', 'StateChanged'),
            ('WorkflowPublishedEventV2', 'StateChanged'),
            ('DeploymentFailedEvent', 'StatusChanged'),
            ('DeploymentRolledBackEvent', 'StatusChanged'),
            ('DeploymentSucceededEvent', 'StatusChanged'),
            ('IterationStateChangedEvent', 'StatusChanged'),
            ('PackageReleasedEvent', 'StatusChanged'),
            ('PackageWithdrawnEvent', 'StatusChanged'),
            ('ProductLifecycleChangedEvent', 'StatusChanged'),
            ('ProductLifecycleChangedEventV2', 'StatusChanged'),
            ('ProgramStatusChangedEvent', 'StatusChanged'),
            ('ProjectPortfolioStatusChangedEvent', 'StatusChanged'),
            ('ProjectStatusChangedEvent', 'StatusChanged'),
            ('ProjectStatusChangedEventV2', 'StatusChanged'),
            ('ReleaseReleasedEvent', 'StatusChanged'),
            ('ReleaseRevertedEvent', 'StatusChanged'),
            ('ReleaseWithdrawnEvent', 'StatusChanged'),
            ('VersionCutEvent', 'StatusChanged'),
            ('VersionReleasedEvent', 'StatusChanged'),
            ('VersionRevertedEvent', 'StatusChanged'),
            ('VersionWithdrawnEvent', 'StatusChanged'),
            ('WorkIterationStateChangedEvent', 'StatusChanged'),
            ('ApplicationRoleUpdatedEvent', 'Updated'),
            ('ApplicationUserUpdatedEvent', 'Updated'),
            ('EnvironmentReclassifiedEvent', 'Updated'),
            ('EnvironmentReclassifiedEventV2', 'Updated'),
            ('IterationDetailsUpdatedEvent', 'Updated'),
            ('IterationTeamChangedEvent', 'Updated'),
            ('IterationUpdatedEvent', 'Updated'),
            ('PackageManifestAmendedEvent', 'Updated'),
            ('ProductDetailsUpdatedEvent', 'Updated'),
            ('ProductLinkedExternallyEvent', 'Updated'),
            ('ProductLinkedExternallyEventV2', 'Updated'),
            ('ProductReparentedEvent', 'Updated'),
            ('ProductReparentedEventV2', 'Updated'),
            ('ProductRetypedEvent', 'Updated'),
            ('ProductRetypedEventV2', 'Updated'),
            ('ProductTagsChangedEvent', 'Updated'),
            ('ProductTagsChangedEventV2', 'Updated'),
            ('ProgramDetailsUpdatedEvent', 'Updated'),
            ('ProgramRolesChangedEvent', 'Updated'),
            ('ProgramStrategicThemesChangedEvent', 'Updated'),
            ('ProjectDetailsUpdatedEvent', 'Updated'),
            ('ProjectKeyChangedEvent', 'Updated'),
            ('ProjectKeyChangedEventV2', 'Updated'),
            ('ProjectLifecycleAssignedEvent', 'Updated'),
            ('ProjectLifecycleAssignedEventV2', 'Updated'),
            ('ProjectLifecycleChangedEvent', 'Updated'),
            ('ProjectLifecycleChangedEventV2', 'Updated'),
            ('ProjectPortfolioDetailsUpdatedEvent', 'Updated'),
            ('ProjectPortfolioRolesChangedEvent', 'Updated'),
            ('ProjectPortfolioScoringModelChangedEvent', 'Updated'),
            ('ProjectReparentedEvent', 'Updated'),
            ('ProjectReparentedEventV2', 'Updated'),
            ('ProjectRolesChangedEvent', 'Updated'),
            ('ProjectRolesChangedEventV2', 'Updated'),
            ('ProjectScoreRecordedEvent', 'Updated'),
            ('ProjectScoreRecordedEventV2', 'Updated'),
            ('ProjectStrategicThemesChangedEvent', 'Updated'),
            ('ProjectStrategicThemesChangedEventV2', 'Updated'),
            ('ReleaseContentsChangedEvent', 'Updated'),
            ('ReleaseDetailsUpdatedEvent', 'Updated'),
            ('StrategicThemeDetailsUpdatedEvent', 'Updated'),
            ('StrategicThemeUpdatedEvent', 'Updated'),
            ('TeamDetailsUpdatedEvent', 'Updated'),
            ('TeamUpdatedEvent', 'Updated'),
            ('VersionDetailsUpdatedEvent', 'Updated'),
            ('WorkflowAssignedEvent', 'Updated'),
            ('WorkflowStatusReclassifiedEvent', 'Updated'),
            ('WorkIterationDetailsUpdatedEvent', 'Updated'),
            ('WorkIterationTeamChangedEvent', 'Updated'),
            ('WorkIterationUpdatedEvent', 'Updated')
            ) AS c (EventType, Category) ON c.EventType = a.EventType;
            """);

        // The old rules, in their old order. 'Cut' was matched case-sensitively so a word merely containing
        // those letters did not badge as a creation.
        migrationBuilder.Sql("""
            UPDATE App.ActivityLogs
            SET Category = CASE
                WHEN LOWER(EventType) LIKE '%health%' THEN 'Health'
                WHEN LOWER(EventType) LIKE '%statuschanged%' THEN 'StatusChanged'
                WHEN LOWER(EventType) LIKE '%activated%' OR LOWER(EventType) LIKE '%archived%' THEN 'StateChanged'
                WHEN LOWER(EventType) LIKE '%deleted%' OR LOWER(EventType) LIKE '%removed%'
                    OR LOWER(EventType) LIKE '%withdrawn%' OR LOWER(EventType) LIKE '%retired%'
                    OR LOWER(EventType) LIKE '%failed%' THEN 'Removed'
                WHEN LOWER(EventType) LIKE '%created%' OR LOWER(EventType) LIKE '%added%'
                    OR EventType COLLATE Latin1_General_CS_AS LIKE '%Cut%' THEN 'Created'
                ELSE 'Updated'
            END
            WHERE Category IS NULL;
            """);

        migrationBuilder.AlterColumn<string>(
            name: "Category",
            schema: "App",
            table: "ActivityLogs",
            type: "varchar(32)",
            maxLength: 32,
            nullable: false,
            oldClrType: typeof(string),
            oldType: "varchar(32)",
            oldMaxLength: 32,
            oldNullable: true);
    }

    /// <inheritdoc />
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "Category",
            schema: "App",
            table: "ActivityLogs");
    }
}
