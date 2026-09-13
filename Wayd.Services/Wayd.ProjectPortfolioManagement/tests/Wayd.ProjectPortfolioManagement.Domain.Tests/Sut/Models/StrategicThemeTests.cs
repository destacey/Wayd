using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Tests.Data;

namespace Wayd.ProjectPortfolioManagement.Domain.Tests.Sut.Models;

public sealed class StrategicThemeTests
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Earlier = Created.Minus(Duration.FromMinutes(1));
    private static readonly Instant Later = Created.Plus(Duration.FromMinutes(1));

    [Fact]
    public void ApplyState_WhenOlderThanTheLastChange_IsSkipped()
    {
        // Arrange — archived at Later, then the earlier activation arrives.
        var theme = new StrategicTheme(new StrategicThemeFaker().WithState(StrategicThemeState.Proposed).Generate(), Earlier);
        theme.ApplyState(StrategicThemeState.Archived, Later);

        // Act
        var applied = theme.ApplyState(StrategicThemeState.Active, Created);

        // Assert
        applied.Should().BeFalse();
        theme.State.Should().Be(StrategicThemeState.Archived);
    }

    [Fact]
    public void ApplyState_WhenRedelivered_HasNothingToApply()
    {
        // Arrange
        var theme = new StrategicTheme(new StrategicThemeFaker().WithState(StrategicThemeState.Proposed).Generate(), Created);
        theme.ApplyState(StrategicThemeState.Active, Later);

        // Act
        var applied = theme.ApplyState(StrategicThemeState.Active, Later);

        // Assert
        applied.Should().BeFalse();
    }

    [Fact]
    public void ApplyDetails_WhenANewerChangeOnlyTouchedState_StillApplies()
    {
        // Arrange
        var theme = new StrategicTheme(new StrategicThemeFaker().WithName("Cloud").WithState(StrategicThemeState.Active).Generate(), Earlier);
        theme.ApplyState(StrategicThemeState.Archived, Later);

        // Act
        var applied = theme.ApplyDetails("Cloud Migration", "Move the estate to managed services.", Created);

        // Assert
        applied.Should().BeTrue();
        theme.Name.Should().Be("Cloud Migration");
        theme.State.Should().Be(StrategicThemeState.Archived);
    }

    [Fact]
    public void ApplyDetails_WhenOlderThanTheLastChange_IsSkipped()
    {
        // Arrange
        var theme = new StrategicTheme(new StrategicThemeFaker().WithName("Cloud Migration").Generate(), Created);

        // Act
        var applied = theme.ApplyDetails("Cloud", "An older description.", Earlier);

        // Assert
        applied.Should().BeFalse();
        theme.Name.Should().Be("Cloud Migration");
    }

    [Fact]
    public void Resync_WhenAGroupAlreadyMatches_KeepsItsWatermark()
    {
        // Arrange
        var source = new StrategicThemeFaker().Generate();
        var theme = new StrategicTheme(source, Created);

        // Act
        var changed = theme.Resync(source, Later);

        // Assert
        changed.Should().BeFalse();
        theme.Watermarks.Should().Be(new StrategicThemeWatermarks(Created, Created));
    }

    [Fact]
    public void Resync_WhenTheCopyHoldsAChangeNewerThanTheRead_LeavesThatGroupAlone()
    {
        // Arrange
        var id = Guid.NewGuid();
        var theme = new StrategicTheme(new StrategicThemeFaker().WithId(id).WithState(StrategicThemeState.Active).Generate(), Earlier);
        theme.ApplyState(StrategicThemeState.Archived, Later);
        var readBeforeTheArchive = new StrategicThemeFaker().WithId(id).WithName(theme.Name).WithDescription(theme.Description).WithState(StrategicThemeState.Active).Generate();

        // Act
        var changed = theme.Resync(readBeforeTheArchive, Created);

        // Assert
        changed.Should().BeFalse();
        theme.State.Should().Be(StrategicThemeState.Archived);
    }
}
