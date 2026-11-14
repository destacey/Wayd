using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Incidents;

namespace Wayd.ProductManagement.Application.Tests.Sut.Incidents;

public sealed class IncidentDurationsTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 4, 1, 12, 0, 0);

    [Fact]
    public void Measure_ShouldLeaveUnstartedStretchesAbsent()
    {
        // Arrange
        var row = Row("Open outage", Now.Minus(Duration.FromHours(2)), null, null, ProductStatusAlias.Open, StatusCategory.Active);

        // Act
        var stretches = IncidentDurations.Measure(row, Now);

        // Assert
        stretches.NoticedToResponseStarted.Should().BeNull();
        stretches.ResponseToRecovered.Should().BeNull();
        stretches.Active.Should().Be(Duration.FromHours(2));
    }

    [Fact]
    public void Measure_ShouldKeepARealZeroWhenResponseStartsAtNotice()
    {
        // Arrange
        var noticed = Now.Minus(Duration.FromHours(1));
        var row = Row("Same instant", noticed, noticed, null, ProductStatusAlias.Responding, StatusCategory.Active);

        // Act
        var stretches = IncidentDurations.Measure(row, Now);

        // Assert
        stretches.NoticedToResponseStarted.Should().Be(Duration.Zero);
        stretches.ResponseToRecovered.Should().BeNull();
        stretches.Active.Should().Be(Duration.FromHours(1));
    }

    [Fact]
    public void Measure_ShouldUseRecoveryAsTheEndOnceTheIncidentIsNoLongerActive()
    {
        // Arrange
        var noticed = Now.Minus(Duration.FromHours(5));
        var started = noticed.Plus(Duration.FromHours(1));
        var recovered = started.Plus(Duration.FromHours(2));
        var row = Row("Recovered", noticed, started, recovered, ProductStatusAlias.Resolved, StatusCategory.Done);

        // Act
        var stretches = IncidentDurations.Measure(row, Now);

        // Assert
        stretches.NoticedToResponseStarted.Should().Be(Duration.FromHours(1));
        stretches.ResponseToRecovered.Should().Be(Duration.FromHours(2));
        stretches.Active.Should().Be(Duration.FromHours(3));
    }

    [Fact]
    public void Measure_ShouldNotGiveACanceledIncidentARecoveryDuration()
    {
        // Arrange
        var noticed = Now.Minus(Duration.FromHours(4));
        var started = noticed.Plus(Duration.FromMinutes(30));
        var row = Row("Set aside", noticed, started, Now, ProductStatusAlias.Canceled, StatusCategory.Removed);

        // Act
        var stretches = IncidentDurations.Measure(row, Now);

        // Assert
        stretches.NoticedToResponseStarted.Should().Be(Duration.FromMinutes(30));
        stretches.ResponseToRecovered.Should().BeNull();
        stretches.Active.Should().BeNull();
    }

    [Fact]
    public void Summarize_ShouldCountActiveIncidentsAndNameTheLongest()
    {
        // Arrange
        var rows = new[]
        {
            Row("Short", Now.Minus(Duration.FromMinutes(20)), null, null, ProductStatusAlias.Open, StatusCategory.Active, key: 2),
            Row("Long", Now.Minus(Duration.FromHours(3)), null, null, ProductStatusAlias.Responding, StatusCategory.Active, key: 9),
            Row("Done alias", Now.Minus(Duration.FromDays(2)), null, null, ProductStatusAlias.Open, StatusCategory.Done, key: 1),
            Row("Removed category", Now.Minus(Duration.FromDays(2)), null, null, ProductStatusAlias.Responding, StatusCategory.Removed, key: 3),
            Row("Closed", Now.Minus(Duration.FromDays(4)), Now.Minus(Duration.FromDays(3)), Now.Minus(Duration.FromDays(1)), ProductStatusAlias.Closed, StatusCategory.Done, key: 4),
            Row("Canceled", Now.Minus(Duration.FromDays(4)), null, null, ProductStatusAlias.Canceled, StatusCategory.Removed, key: 5),
        };

        // Act
        var figures = IncidentDurations.Summarize(rows, Now);

        // Assert
        figures.ActiveCount.Should().Be(2);
        figures.LongestActive.Should().NotBeNull();
        figures.LongestActive!.Value.Key.Should().Be(9);
        figures.LongestActive.Value.Title.Should().Be("Long");
        figures.LongestActive.Value.Active.Should().Be(Duration.FromHours(3));
    }

    [Fact]
    public void Summarize_ShouldBreakALongestTieOnTheLowerKey()
    {
        // Arrange
        var noticed = Now.Minus(Duration.FromHours(1));
        var rows = new[]
        {
            Row("Higher key", noticed, null, null, ProductStatusAlias.Open, StatusCategory.Active, key: 8),
            Row("Lower key", noticed, null, null, ProductStatusAlias.Open, StatusCategory.Active, key: 3),
        };

        // Act
        var figures = IncidentDurations.Summarize(rows, Now);

        // Assert
        figures.LongestActive!.Value.Key.Should().Be(3);
        figures.LongestActive.Value.Title.Should().Be("Lower key");
    }

    [Fact]
    public void Summarize_ShouldAverageClosedRecoveriesAndLeaveTheMeanAbsentWhenNoneQualify()
    {
        // Arrange
        var rows = new[]
        {
            Row("One hour", Now.Minus(Duration.FromHours(4)), Now.Minus(Duration.FromHours(3)), Now.Minus(Duration.FromHours(3)), ProductStatusAlias.Closed, StatusCategory.Done),
            Row("Three hours", Now.Minus(Duration.FromHours(5)), Now.Minus(Duration.FromHours(4)), Now.Minus(Duration.FromHours(2)), ProductStatusAlias.Closed, StatusCategory.Done),
            Row("Still open", Now.Minus(Duration.FromHours(9)), null, Now, ProductStatusAlias.Open, StatusCategory.Active),
            Row("Canceled with a recovery stamp", Now.Minus(Duration.FromHours(8)), Now.Minus(Duration.FromHours(7)), Now, ProductStatusAlias.Canceled, StatusCategory.Removed),
            Row("Recovered, not closed", Now.Minus(Duration.FromHours(6)), Now.Minus(Duration.FromHours(5)), Now.Minus(Duration.FromHours(1)), ProductStatusAlias.Resolved, StatusCategory.Done),
            Row("Closed without recovery", Now.Minus(Duration.FromHours(2)), null, null, ProductStatusAlias.Closed, StatusCategory.Done),
        };

        // Act
        var figures = IncidentDurations.Summarize(rows, Now);
        var empty = IncidentDurations.Summarize([], Now);

        // Assert
        figures.MeanTimeToRecover.Should().Be(Duration.FromHours(2));
        figures.ActiveCount.Should().Be(1);
        empty.ActiveCount.Should().Be(0);
        empty.LongestActive.Should().BeNull();
        empty.MeanTimeToRecover.Should().BeNull();
    }

    private static IncidentDurations.Row Row(
        string title,
        Instant noticed,
        Instant? started,
        Instant? recovered,
        ProductStatusAlias alias,
        StatusCategory category,
        int key = 1) =>
        new(key, title, noticed, started, recovered, alias, category);
}
