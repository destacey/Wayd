using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.StatusWorkflows.Queries;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows.Queries;

/// <summary>
/// Covers <see cref="GetTimeInStatusReportQueryHandler"/> — how long records of one type stay in each status.
/// </summary>
public sealed class GetTimeInStatusReportQueryHandlerTests : StatusWorkflowHandlerTestBase
{
    private static readonly Instant WindowStart = Instant.FromUtc(2026, 3, 1, 0, 0);
    private static readonly Instant WindowEnd = Instant.FromUtc(2026, 3, 31, 0, 0);

    private GetTimeInStatusReportQueryHandler CreateSut() => new(DbContext);

    private static long Days(double days) => (long)Duration.FromDays(days).TotalSeconds;

    private StatusWorkflow SeedAssignedWorkflow()
    {
        var workflow = SeedWorkflow(publish: true);
        var assignment = WorkflowAssignment.Create(Widget.Key, null, workflow, EventActor.System, Now).Value;
        assignment.ClearDomainEvents();
        DbContext.AddWorkflowAssignment(assignment);

        return workflow;
    }

    private static StatusRef Ref(StatusWorkflow workflow, string name)
    {
        var status = workflow.Statuses.Single(s => s.Name == name);

        return new StatusRef(workflow.Id, status.Id, status.Name, status.Category, status.Alias);
    }

    /// <summary>Walks one record through statuses, one transition per (status, instant) step.</summary>
    private void SeedHistory(Guid recordId, string ownerType, params (StatusRef Status, Instant On)[] steps)
    {
        StatusRef? from = null;

        for (var i = 0; i < steps.Length; i++)
        {
            DbContext.AddStatusTransition(new StatusTransition(
                ownerType, recordId, from, steps[i].Status, EventActor.System, steps[i].On, i));
            from = steps[i].Status;
        }
    }

    private void SeedHistory(Guid recordId, params (StatusRef Status, Instant On)[] steps) =>
        SeedHistory(recordId, Widget.Key, steps);

    private static Instant March(int day) => Instant.FromUtc(2026, 3, day, 0, 0);

    [Fact]
    public async Task Handle_AveragesAndMediansTheStaysThatEndedInTheWindow()
    {
        // Arrange — Notable stays of 2, 4 and 9 days end in the window. Mean 5 days, median 4.
        var workflow = SeedAssignedWorkflow();
        var proposed = Ref(workflow, "Proposed");
        var notable = Ref(workflow, "Notable");
        var terminal = Ref(workflow, "Terminal");

        SeedHistory(Guid.CreateVersion7(), (proposed, March(1)), (notable, March(3)), (terminal, March(5)));
        SeedHistory(Guid.CreateVersion7(), (proposed, March(1)), (notable, March(6)), (terminal, March(10)));
        SeedHistory(Guid.CreateVersion7(), (notable, March(10)), (terminal, March(19)));

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new GetTimeInStatusReportQuery(Widget.Key, WindowStart, WindowEnd), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var row = result.Value.Statuses.Single(r => r.StatusId == notable.StatusId);
        row.Visits.Should().Be(3);
        row.AverageSeconds.Should().Be(Days(5));
        row.MedianSeconds.Should().Be(Days(4));

        var proposedRow = result.Value.Statuses.Single(r => r.StatusId == proposed.StatusId);
        proposedRow.Visits.Should().Be(2);
        proposedRow.AverageSeconds.Should().Be(Days(3.5));
        proposedRow.MedianSeconds.Should().Be(Days(3.5), "an even count takes the mean of the middle two");
    }

    [Fact]
    public async Task Handle_CountsAStayOnlyWhenItEndsInsideTheWindow()
    {
        // Arrange — Notable left before the window, Notable left after it, and Notable never left. Only
        // the stay that began before the window and ended inside it counts, at its full 20-day length.
        // That record's earlier Proposed stay ended before the window, so it does not count either.
        var workflow = SeedAssignedWorkflow();
        var proposed = Ref(workflow, "Proposed");
        var notable = Ref(workflow, "Notable");
        var terminal = Ref(workflow, "Terminal");

        SeedHistory(Guid.CreateVersion7(), (notable, Instant.FromUtc(2026, 2, 1, 0, 0)), (terminal, Instant.FromUtc(2026, 2, 10, 0, 0)));
        SeedHistory(Guid.CreateVersion7(), (notable, March(20)), (terminal, Instant.FromUtc(2026, 4, 5, 0, 0)));
        SeedHistory(Guid.CreateVersion7(), (notable, March(2)));
        SeedHistory(
            Guid.CreateVersion7(),
            (proposed, Instant.FromUtc(2026, 2, 1, 0, 0)),
            (notable, Instant.FromUtc(2026, 2, 20, 0, 0)),
            (terminal, March(12)));

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new GetTimeInStatusReportQuery(Widget.Key, WindowStart, WindowEnd), TestContext.Current.CancellationToken);

        // Assert
        var row = result.Value.Statuses.Single(r => r.StatusId == notable.StatusId);
        row.Visits.Should().Be(1);
        row.AverageSeconds.Should().Be(Days(20));
        result.Value.Statuses.Single(r => r.StatusId == proposed.StatusId).Visits.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ListsEveryWorkflowStatusInOrderWithEmptyRowsForUnvisitedOnes()
    {
        // Arrange
        var workflow = SeedAssignedWorkflow();
        SeedHistory(Guid.CreateVersion7(), (Ref(workflow, "Proposed"), March(1)), (Ref(workflow, "Notable"), March(4)));

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new GetTimeInStatusReportQuery(Widget.Key, WindowStart, WindowEnd), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Workflow!.Id.Should().Be(workflow.Id);
        result.Value.Statuses.Select(r => (r.Name, r.Visits, r.InCurrentWorkflow)).Should().Equal(
            ("Proposed", 1, true), ("Notable", 0, true), ("Terminal", 0, true));
        result.Value.Statuses[1].AverageSeconds.Should().BeNull();
        result.Value.Statuses[1].MedianSeconds.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReportsAStatusFromAnEarlierWorkflowUnderItsFrozenName()
    {
        // Arrange — a stay in an old workflow's "Live", ended by a move within that workflow.
        SeedAssignedWorkflow();
        var oldWorkflowId = Guid.CreateVersion7();
        var live = new StatusRef(oldWorkflowId, Guid.CreateVersion7(), "Live", StatusCategory.Active, StatusWorkflow.NoAlias);
        var gone = new StatusRef(oldWorkflowId, Guid.CreateVersion7(), "Gone", StatusCategory.Done, StatusWorkflow.NoAlias);
        SeedHistory(Guid.CreateVersion7(), (live, March(1)), (gone, March(8)));

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new GetTimeInStatusReportQuery(Widget.Key, WindowStart, WindowEnd), TestContext.Current.CancellationToken);

        // Assert
        var row = result.Value.Statuses.Single(r => r.StatusId == live.StatusId);
        row.Name.Should().Be("Live");
        row.InCurrentWorkflow.Should().BeFalse();
        row.Visits.Should().Be(1);
        row.AverageSeconds.Should().Be(Days(7));
    }

    [Fact]
    public async Task Handle_LeavesOutStaysEndedOrBegunByAWorkflowReassignment()
    {
        // Arrange — the record sat in an old workflow's "Live" until a reassignment moved it onto
        // Notable on 8 March, then left Notable on 10 March. Neither stay measures its status.
        var workflow = SeedAssignedWorkflow();
        var live = new StatusRef(Guid.CreateVersion7(), Guid.CreateVersion7(), "Live", StatusCategory.Active, StatusWorkflow.NoAlias);
        var notable = Ref(workflow, "Notable");
        SeedHistory(Guid.CreateVersion7(), (live, March(1)), (notable, March(8)), (Ref(workflow, "Terminal"), March(10)));

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new GetTimeInStatusReportQuery(Widget.Key, WindowStart, WindowEnd), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Statuses.Should().NotContain(r => r.StatusId == live.StatusId);
        result.Value.Statuses.Single(r => r.StatusId == notable.StatusId).Visits.Should().Be(0);
    }

    [Fact]
    public async Task Handle_IgnoresAnotherOwnerTypesHistory()
    {
        // Arrange — the same statuses under another owner type. Unregistered: the query reads only its own.
        var workflow = SeedAssignedWorkflow();
        var recordId = Guid.CreateVersion7();
        SeedHistory(recordId, "test.report-elsewhere", (Ref(workflow, "Proposed"), March(1)), (Ref(workflow, "Notable"), March(4)));

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new GetTimeInStatusReportQuery(Widget.Key, WindowStart, WindowEnd), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Statuses.Should().OnlyContain(r => r.Visits == 0);
    }

    [Fact]
    public async Task Handle_WithNoAssignedWorkflow_ReportsTheStatusesFoundInHistory()
    {
        // Arrange
        var proposed = new StatusRef(Guid.CreateVersion7(), Guid.CreateVersion7(), "Proposed", StatusCategory.Proposed, StatusWorkflow.NoAlias);
        var active = new StatusRef(proposed.WorkflowId, Guid.CreateVersion7(), "Active", StatusCategory.Active, StatusWorkflow.NoAlias);
        SeedHistory(Guid.CreateVersion7(), (proposed, March(1)), (active, March(2)));

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new GetTimeInStatusReportQuery(Widget.Key, WindowStart, WindowEnd), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Workflow.Should().BeNull();
        var row = result.Value.Statuses.Should().ContainSingle().Subject;
        row.Name.Should().Be("Proposed");
        row.InCurrentWorkflow.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_FailsForAnUnregisteredOwnerType()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new GetTimeInStatusReportQuery("test.unknown", WindowStart, WindowEnd), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
    }
}
