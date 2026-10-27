using NodaTime;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Common.Models;
using Wayd.DemandManagement.Domain;
using Wayd.DemandManagement.Domain.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.StatusWorkflows;

namespace Wayd.Infrastructure.IntegrationTests.Sut.StatusWorkflows;

/// <summary>
/// The intake request migrator against SQL Server: a reassignment that would move requests is refused, and
/// one that moves none goes through.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class IntakeRequestStatusRecordMigratorTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 1, 8, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static StatusRemap Remap()
    {
        DemandManagementWorkflowOwners.Register();

        StatusWorkflow Workflow(string name)
        {
            var workflow = StatusWorkflow.Create(name, null, DemandManagementWorkflowOwners.IntakeRequest.Key, EventActor.System, Now).Value;
            workflow.AddStatus("Submitted", null, StatusCategory.Proposed, (int)IntakeRequestStatusAlias.Submitted, EventActor.System, Now);
            workflow.AddStatus("Withdrawn", null, StatusCategory.Removed, (int)IntakeRequestStatusAlias.Withdrawn, EventActor.System, Now);
            return workflow;
        }

        return StatusRemap.AutoMap(Workflow("From"), Workflow("To")).Value;
    }

    [Fact]
    public async Task Migrate_WithNoRequestOnTheWorkflow_MovesNothingAndSucceeds()
    {
        // Arrange
        var remap = Remap();
        await using var context = _fixture.CreateContext();

        // Act
        var result = await new IntakeRequestStatusRecordMigrator(context)
            .Migrate(remap, null, EventActor.System, Now, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(0);
    }

    [Fact]
    public async Task Migrate_WithARequestOnTheWorkflow_IsRefusedAndCountsIt()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var remap = Remap();
        await using (var seeding = _fixture.CreateContext())
        {
            var requester = Employee.Create(new PersonName("Intake", null, "Probe"), $"{Guid.NewGuid():N}"[..12], Now,
                new EmailAddress($"{Guid.NewGuid():N}"[..12] + "@acme.example"), null, null, null, null, true, null, EventActor.System, Now);
            var desk = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.Open, true, true, true, [],
                EventActor.System, Now);
            var channel = IntakeChannel.Create($"Channel-{Guid.NewGuid():N}"[..30], null, 1, EventActor.System, Now);
            seeding.Employees.Add(requester);
            seeding.IntakeDesks.Add(desk);
            seeding.IntakeChannels.Add(channel);
            await seeding.SaveChangesAsync(ct);
            seeding.IntakeRequests.Add(IntakeRequest.Create("Title", "Description", null, requester.Id, requester.Id, desk, channel,
                null, null, IntakeStatuses.Submitted(remap.FromWorkflowId), EventActor.System, Now).Value);
            await seeding.SaveChangesAsync(ct);
        }

        await using var context = _fixture.CreateContext();
        var sut = new IntakeRequestStatusRecordMigrator(context);

        // Act
        var migrated = await sut.Migrate(remap, null, EventActor.System, Now, ct);
        var counted = await sut.CountByStatus(remap.FromWorkflowId, null, ct);

        // Assert
        migrated.IsFailure.Should().BeTrue();
        migrated.Error.Should().Be(IntakeRequestStatusRecordMigrator.RequestsCannotMoveYetError);
        counted.Value.Values.Sum().Should().Be(1);
    }
}
