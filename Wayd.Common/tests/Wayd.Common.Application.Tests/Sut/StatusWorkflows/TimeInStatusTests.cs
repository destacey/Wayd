using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;

namespace Wayd.Common.Application.Tests.Sut.StatusWorkflows;

/// <summary>
/// Covers <see cref="TimeInStatus"/> — turning a record's status history into time spent per status.
/// </summary>
public sealed class TimeInStatusTests
{
    private static readonly Guid Concept = Guid.CreateVersion7();
    private static readonly Guid Active = Guid.CreateVersion7();
    private static readonly Guid Sunset = Guid.CreateVersion7();
    private static readonly Guid Workflow = Guid.CreateVersion7();

    private static Instant On(int month, int day) => Instant.FromUtc(2026, month, day, 0, 0);

    private static StatusEntry Entry(
        int sequence, Guid statusId, string name, StatusCategory category, Instant on, Guid? workflowId = null) =>
        new(sequence, workflowId ?? Workflow, statusId, name, category, on);

    [Fact]
    public void Calculate_MeasuresEachVisitToTheNextTransitionAndTheCurrentOneToNow()
    {
        // Arrange — created in Concept and moved to Active on 1 March, then to Sunset on 1 June.
        var entries = new[]
        {
            Entry(1, Concept, "Concept", StatusCategory.Proposed, On(3, 1)),
            Entry(2, Active, "Active", StatusCategory.Active, On(3, 1)),
            Entry(3, Sunset, "Sunset", StatusCategory.Active, On(6, 1)),
        };

        // Act
        var result = TimeInStatus.Calculate(entries, On(7, 11));

        // Assert
        result.Visits.Select(v => (v.StatusName, v.Duration, v.LeftOn)).Should().Equal(
            ("Concept", Duration.Zero, On(3, 1)),
            ("Active", Duration.FromDays(92), On(6, 1)),
            ("Sunset", Duration.FromDays(40), (Instant?)null));
        result.Visits[^1].IsCurrent.Should().BeTrue();
        result.Visits.Should().OnlyContain(v => !v.TouchesWorkflowSwitch);
    }

    [Fact]
    public void Calculate_OrdersEntriesBySequenceNotByArrival()
    {
        // Arrange — supplied newest first, as the history reader returns them.
        var entries = new[]
        {
            Entry(3, Sunset, "Sunset", StatusCategory.Active, On(6, 1)),
            Entry(2, Active, "Active", StatusCategory.Active, On(3, 1)),
            Entry(1, Concept, "Concept", StatusCategory.Proposed, On(2, 1)),
        };

        // Act
        var result = TimeInStatus.Calculate(entries, On(7, 1));

        // Assert
        result.Visits.Select(v => v.Sequence).Should().Equal(1, 2, 3);
        result.Visits[0].Duration.Should().Be(Duration.FromDays(28));
        result.Visits[^1].StatusId.Should().Be(Sunset);
    }

    [Fact]
    public void Calculate_ReturningToAStatus_CountsEachVisitAndTotalsThem()
    {
        // Arrange — Active for 10 days, Concept for 5, then Active again until now (20 days).
        var entries = new[]
        {
            Entry(1, Active, "Active", StatusCategory.Active, On(3, 1)),
            Entry(2, Concept, "Concept", StatusCategory.Proposed, On(3, 11)),
            Entry(3, Active, "Active", StatusCategory.Active, On(3, 16)),
        };

        // Act
        var result = TimeInStatus.Calculate(entries, On(4, 5));

        // Assert
        result.Visits.Where(v => v.StatusId == Active).Select(v => v.Duration)
            .Should().Equal(Duration.FromDays(10), Duration.FromDays(20));
        result.ByStatus.Should().Equal(
            new StatusTimeTotal(Active, "Active", StatusCategory.Active, 2, Duration.FromDays(30)),
            new StatusTimeTotal(Concept, "Concept", StatusCategory.Proposed, 1, Duration.FromDays(5)));
    }

    [Fact]
    public void Calculate_FlagsTheStaysEndedAndBegunByAWorkflowSwitch()
    {
        // Arrange — Concept then Active in one workflow, remapped onto another's Sunset, then Retired.
        var other = Guid.CreateVersion7();
        var retired = Guid.CreateVersion7();
        var entries = new[]
        {
            Entry(1, Concept, "Concept", StatusCategory.Proposed, On(3, 1)),
            Entry(2, Active, "Active", StatusCategory.Active, On(3, 4)),
            Entry(3, Sunset, "Sunset", StatusCategory.Active, On(3, 10), other),
            Entry(4, retired, "Retired", StatusCategory.Done, On(3, 15), other),
        };

        // Act
        var result = TimeInStatus.Calculate(entries, On(3, 20));

        // Assert — Active ended at the switch and Sunset began there; the others never touched it.
        result.Visits.Select(v => v.TouchesWorkflowSwitch).Should().Equal(false, true, true, false);
    }

    [Fact]
    public void Calculate_AfterAWorkflowReassignment_KeepsEarlierVisitsUnderTheirOldStatuses()
    {
        // Arrange — remapped from the old workflow's "Live" onto the new workflow's "Active".
        var live = Guid.CreateVersion7();
        var entries = new[]
        {
            Entry(1, live, "Live", StatusCategory.Active, On(3, 1), Guid.CreateVersion7()),
            Entry(2, Active, "Active", StatusCategory.Active, On(3, 15)),
        };

        // Act
        var result = TimeInStatus.Calculate(entries, On(3, 20));

        // Assert
        result.ByStatus.Should().Equal(
            new StatusTimeTotal(live, "Live", StatusCategory.Active, 1, Duration.FromDays(14)),
            new StatusTimeTotal(Active, "Active", StatusCategory.Active, 1, Duration.FromDays(5)));
    }

    [Fact]
    public void Calculate_StatusRenamedBetweenVisits_TotalsUnderTheLatestName()
    {
        // Arrange — same status id, frozen under two names.
        var entries = new[]
        {
            Entry(1, Active, "Live", StatusCategory.Active, On(3, 1)),
            Entry(2, Concept, "Concept", StatusCategory.Proposed, On(3, 2)),
            Entry(3, Active, "Active", StatusCategory.Active, On(3, 3)),
        };

        // Act
        var result = TimeInStatus.Calculate(entries, On(3, 5));

        // Assert
        result.Visits[0].StatusName.Should().Be("Live");
        result.ByStatus[0].Should().Be(
            new StatusTimeTotal(Active, "Active", StatusCategory.Active, 2, Duration.FromDays(3)));
    }

    [Fact]
    public void Calculate_NeverReportsNegativeTime()
    {
        // Arrange — the second entry is recorded before the first, and now is before both.
        var entries = new[]
        {
            Entry(1, Concept, "Concept", StatusCategory.Proposed, On(3, 10)),
            Entry(2, Active, "Active", StatusCategory.Active, On(3, 5)),
        };

        // Act
        var result = TimeInStatus.Calculate(entries, On(3, 1));

        // Assert
        result.Visits.Select(v => v.Duration).Should().Equal(Duration.Zero, Duration.Zero);
    }

    [Fact]
    public void Calculate_NoHistory_ReturnsNoVisitsOrTotals()
    {
        // Act
        var result = TimeInStatus.Calculate([], On(3, 1));

        // Assert
        result.Visits.Should().BeEmpty();
        result.ByStatus.Should().BeEmpty();
    }
}
