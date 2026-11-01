using CSharpFunctionalExtensions;
using NodaTime;
using Moq;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows;

/// <summary>
/// An owner type with record types, its Bug and Story, and a migrator that records what it was asked to move.
/// </summary>
public abstract class WorkflowSchemeHandlerTestBase : StatusWorkflowHandlerTestBase
{
    protected static readonly WorkflowOwnerDescriptor Ticket = new(
        "test.schemes.ticket",
        "Ticket",
        new Dictionary<int, WorkflowAlias> { [NotableAlias] = new("Notable", StatusCategory.Active), [TerminalAlias] = new("Terminal", StatusCategory.Done) },
        [NotableAlias, TerminalAlias],
        hasRecordTypes: true);

    protected readonly Mock<IRecordTypeStatusMigrator> Migrator = new();
    protected readonly List<(StatusRemap Remap, RecordSelection Selection)> Migrated = [];
    protected readonly RecordType Bug;
    protected readonly RecordType Story;

    protected WorkflowSchemeHandlerTestBase()
    {
        WorkflowOwners.Register(Ticket);

        Migrator.SetupGet(m => m.OwnerType).Returns(Ticket.Key);
        Migrator
            .Setup(m => m.Migrate(It.IsAny<StatusRemap>(), It.IsAny<RecordSelection>(), It.IsAny<EventActor>(), It.IsAny<Instant>(), It.IsAny<CancellationToken>()))
            .Callback<StatusRemap, RecordSelection, EventActor, Instant, CancellationToken>((remap, selection, _, _, _) => Migrated.Add((remap, selection)))
            .ReturnsAsync(Result.Success(3));

        Bug = RecordType.Create(Ticket.Key, "Bug", EventActor.System, Now).Value;
        Story = RecordType.Create(Ticket.Key, "Story", EventActor.System, Now).Value;
        DbContext.AddRecordType(Bug);
        DbContext.AddRecordType(Story);
    }

    protected StatusWorkflow TicketWorkflow(string name) => SeedWorkflow(name, publish: true, owner: Ticket);

    protected StatusWorkflowScheme Scheme(StatusWorkflow defaultWorkflow, string name = "Engineering")
    {
        var scheme = StatusWorkflowScheme.Create(Ticket.Key, name, null, defaultWorkflow, EventActor.System, Now).Value;
        DbContext.AddStatusWorkflowScheme(scheme);

        return scheme;
    }

    protected WorkflowAssignment Assign(StatusWorkflowScheme scheme, StatusWorkflow defaultWorkflow, Guid? scopeId)
    {
        var assignment = WorkflowAssignment.Create(Ticket.Key, scopeId, scheme, defaultWorkflow, EventActor.System, Now).Value;
        assignment.ClearDomainEvents();
        DbContext.AddWorkflowAssignment(assignment);

        return assignment;
    }

    protected static StatusRemap Remap(StatusWorkflow from, StatusWorkflow to) => StatusRemap.AutoMap(from, to).Value;
}
