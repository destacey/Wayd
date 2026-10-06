using NodaTime;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;

namespace Wayd.Work.Domain.Tests.Sut.Models;

public sealed class WorkItemStateTimelineTests
{
    private static readonly Guid WorkItemId = Guid.NewGuid();
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly Instant Start = Instant.FromUtc(2026, 3, 1, 9, 0);

    [Fact]
    public void Apply_FirstRevision_OpensAnOpenPeriodAtTheRevision()
    {
        // Arrange
        var timeline = NewTimeline();
        var state = new WorkItemTrackedStateFaker().Generate();

        // Act
        var period = timeline.Apply(1, Start, state);

        // Assert
        period.Should().NotBeNull();
        period!.WorkItemId.Should().Be(WorkItemId);
        period.WorkspaceId.Should().Be(WorkspaceId);
        period.Revision.Should().Be(1);
        period.ValidFrom.Should().Be(Start);
        period.ValidTo.Should().BeNull();
        period.State.Should().Be(state);
    }

    [Fact]
    public void Apply_RevisionThatChangesNoTrackedField_OpensNoPeriod()
    {
        // Arrange
        var timeline = NewTimeline();
        var state = new WorkItemTrackedStateFaker().Generate();
        var first = timeline.Apply(1, Start, state)!;

        // Act
        var period = timeline.Apply(2, Start.Plus(Duration.FromHours(1)), state);

        // Assert
        period.Should().BeNull();
        first.ValidTo.Should().BeNull();
    }

    [Fact]
    public void Apply_RevisionThatChangesATrackedField_ClosesTheOpenPeriodWhereTheNewOneBegins()
    {
        // Arrange
        var timeline = NewTimeline();
        var state = new WorkItemTrackedStateFaker().WithStatusName("New").Generate();
        var first = timeline.Apply(1, Start, state)!;
        var changed = Start.Plus(Duration.FromDays(2));

        // Act
        var second = timeline.Apply(2, changed, state with { StatusName = "Active" });

        // Assert
        first.ValidTo.Should().Be(changed);
        second.Should().NotBeNull();
        second!.ValidFrom.Should().Be(changed);
        second.ValidTo.Should().BeNull();
        second.StatusName.Should().Be("Active");
    }

    [Fact]
    public void Apply_RevisionWhoseOnlyDifferenceIsAResolvedId_OpensNoPeriod()
    {
        // Arrange — an admin mapped the assignee between syncs, so the same source value now
        // resolves to an employee
        var timeline = NewTimeline();
        var state = new WorkItemTrackedStateFaker().Generate();
        timeline.Apply(1, Start, state with { AssignedToId = null });

        // Act
        var period = timeline.Apply(2, Start.Plus(Duration.FromHours(1)), state);

        // Assert
        period.Should().BeNull();
    }

    [Fact]
    public void Apply_RevisionThatOnlyRecasesTheStatusOrTypeName_OpensNoPeriod()
    {
        // Arrange
        var timeline = NewTimeline();
        var state = new WorkItemTrackedStateFaker().WithStatusName("Active").Generate();
        timeline.Apply(1, Start, state);

        // Act
        var period = timeline.Apply(2, Start.Plus(Duration.FromHours(1)),
            state with { StatusName = "ACTIVE", WorkTypeName = state.WorkTypeName.ToLowerInvariant() });

        // Assert
        period.Should().BeNull();
    }

    [Fact]
    public void Apply_RevisionThatOnlyRecasesTheAssigneeId_OpensAPeriod()
    {
        // Arrange — identity ids are opaque, so a different case is a different identity
        var timeline = NewTimeline();
        var state = new WorkItemTrackedStateFaker().Generate() with { AssignedToExternalId = "abc-identity" };
        timeline.Apply(1, Start, state);

        // Act
        var period = timeline.Apply(2, Start.Plus(Duration.FromHours(1)), state with { AssignedToExternalId = "ABC-IDENTITY" });

        // Assert
        period.Should().NotBeNull();
    }

    [Fact]
    public void Apply_RevisionAlreadyApplied_IsSkipped()
    {
        // Arrange — a batch delivered again after its watermark failed to save
        var timeline = NewTimeline();
        var state = new WorkItemTrackedStateFaker().WithStatusName("New").Generate();
        timeline.Apply(1, Start, state);
        var second = timeline.Apply(2, Start.Plus(Duration.FromDays(1)), state with { StatusName = "Active" })!;

        // Act
        var replayed = timeline.Apply(2, Start.Plus(Duration.FromDays(1)), state with { StatusName = "Active" });

        // Assert
        replayed.Should().BeNull();
        second.ValidTo.Should().BeNull();
    }

    [Fact]
    public void Apply_RevisionAtOrBelowTheStoredLastRevision_IsSkipped()
    {
        // Arrange — the item's periods were saved by an earlier sync, the last opened by revision 5
        var open = NewTimeline().Apply(5, Start, new WorkItemTrackedStateFaker().WithStatusName("Active").Generate())!;
        var timeline = new WorkItemStateTimeline(WorkItemId, WorkspaceId, open, lastRevision: 5);

        // Act
        var period = timeline.Apply(4, Start.Minus(Duration.FromDays(1)), open.State with { StatusName = "New" });

        // Assert
        period.Should().BeNull();
        open.ValidTo.Should().BeNull();
    }

    [Fact]
    public void Apply_RevisionStampedBeforeTheOpenPeriod_StartsWhereTheOpenPeriodStarts()
    {
        // Arrange — clock skew in the source
        var timeline = NewTimeline();
        var state = new WorkItemTrackedStateFaker().WithStatusName("New").Generate();
        var first = timeline.Apply(1, Start, state)!;

        // Act
        var second = timeline.Apply(2, Start.Minus(Duration.FromMinutes(5)), state with { StatusName = "Active" });

        // Assert
        first.ValidTo.Should().Be(Start);
        second!.ValidFrom.Should().Be(Start);
    }

    [Fact]
    public void Apply_ASequenceOfRevisions_ReproducesTheTrackedStateAtAnyInstant()
    {
        // Arrange — created in sprint 10 as New, activated, title edited (untracked), moved to sprint 11
        var created = new WorkItemTrackedStateFaker().WithStatusName("New").WithExternalIterationId(10).Generate();
        var activated = created with { StatusName = "Active" };
        var moved = activated with { ExternalIterationId = 11, IterationId = Guid.NewGuid() };
        var revisions = new (int Revision, Instant Changed, WorkItemTrackedState State)[]
        {
            (1, Start, created),
            (2, Start.Plus(Duration.FromDays(2)), activated),
            (3, Start.Plus(Duration.FromDays(4)), activated),
            (4, Start.Plus(Duration.FromDays(8)), moved),
        };

        // Act
        var periods = Replay(revisions);

        // Assert
        periods.Should().HaveCount(3);
        StateAt(periods, Start).Should().Be(created);
        StateAt(periods, Start.Plus(Duration.FromDays(1))).Should().Be(created);
        StateAt(periods, Start.Plus(Duration.FromDays(2))).Should().Be(activated);
        StateAt(periods, Start.Plus(Duration.FromDays(5))).Should().Be(activated);
        StateAt(periods, Start.Plus(Duration.FromDays(8))).Should().Be(moved);
        StateAt(periods, Start.Plus(Duration.FromDays(100))).Should().Be(moved);
        StateAt(periods, Start.Minus(Duration.FromSeconds(1))).Should().BeNull();
    }

    [Fact]
    public void Apply_ReplayingTheSameRevisionsFromEmpty_ProducesTheSamePeriods()
    {
        // Arrange — a full sync clears the history and replays every revision
        var state = new WorkItemTrackedStateFaker().WithStatusName("New").Generate();
        var revisions = new (int Revision, Instant Changed, WorkItemTrackedState State)[]
        {
            (1, Start, state),
            (2, Start.Plus(Duration.FromDays(1)), state with { StatusName = "Active" }),
            (3, Start.Plus(Duration.FromDays(3)), state with { StatusName = "Closed" }),
        };
        var first = Replay(revisions);

        // Act
        var rebuilt = Replay(revisions);

        // Assert
        rebuilt.Select(Snapshot).Should().Equal(first.Select(Snapshot));
    }

    private static WorkItemStateTimeline NewTimeline() => new(WorkItemId, WorkspaceId, openPeriod: null, lastRevision: 0);

    private static List<WorkItemStateHistory> Replay(IEnumerable<(int Revision, Instant Changed, WorkItemTrackedState State)> revisions)
    {
        var timeline = NewTimeline();
        var periods = new List<WorkItemStateHistory>();
        foreach (var (revision, changed, state) in revisions)
        {
            var period = timeline.Apply(revision, changed, state);
            if (period is not null)
                periods.Add(period);
        }
        return periods;
    }

    private static WorkItemTrackedState? StateAt(IEnumerable<WorkItemStateHistory> periods, Instant instant) =>
        periods.SingleOrDefault(p => p.ValidFrom <= instant && (p.ValidTo is null || p.ValidTo > instant))?.State;

    private static (int, Instant, Instant?, WorkItemTrackedState) Snapshot(WorkItemStateHistory period) =>
        (period.Revision, period.ValidFrom, period.ValidTo, period.State);
}
