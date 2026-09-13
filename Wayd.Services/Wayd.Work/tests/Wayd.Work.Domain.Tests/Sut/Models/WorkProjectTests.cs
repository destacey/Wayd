using NodaTime;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;

namespace Wayd.Work.Domain.Tests.Sut.Models;

public sealed class WorkProjectTests
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Earlier = Created.Minus(Duration.FromMinutes(1));
    private static readonly Instant Later = Created.Plus(Duration.FromMinutes(1));

    [Fact]
    public void ApplyDetails_NeverWritesTheKey()
    {
        // Arrange — rekeyed at Later; a details change from Created still carries the old key in its payload.
        var project = new WorkProject(new WorkProjectFaker().WithKey(new ProjectKey("OLDKEY")).Generate(), Earlier);
        project.ApplyKey(new ProjectKey("NEWKEY"), Later);

        // Act
        var applied = project.ApplyDetails("Atlas", "Consolidates the regional trackers.", Created);

        // Assert
        applied.Should().BeTrue();
        project.Name.Should().Be("Atlas");
        project.Key.Value.Should().Be("NEWKEY");
    }

    [Fact]
    public void ApplyDetails_WhenOlderThanTheLastChange_IsSkipped()
    {
        // Arrange
        var project = new WorkProject(new WorkProjectFaker().WithName("Atlas").Generate(), Created);

        // Act
        var applied = project.ApplyDetails("Borealis", "An older description.", Earlier);

        // Assert
        applied.Should().BeFalse();
        project.Name.Should().Be("Atlas");
    }

    [Fact]
    public void ApplyKey_WhenOlderThanTheLastChange_IsSkipped()
    {
        // Arrange — rekeyed twice, delivered newest first.
        var project = new WorkProject(new WorkProjectFaker().WithKey(new ProjectKey("FIRST")).Generate(), Earlier);
        project.ApplyKey(new ProjectKey("THIRD"), Later);

        // Act
        var applied = project.ApplyKey(new ProjectKey("SECOND"), Created);

        // Assert
        applied.Should().BeFalse();
        project.Key.Value.Should().Be("THIRD");
    }

    [Fact]
    public void ApplyKey_WhenRedelivered_HasNothingToApply()
    {
        // Arrange
        var project = new WorkProject(new WorkProjectFaker().Generate(), Created);
        project.ApplyKey(new ProjectKey("NEWKEY"), Later);

        // Act
        var applied = project.ApplyKey(new ProjectKey("NEWKEY"), Later);

        // Assert
        applied.Should().BeFalse();
    }

    [Fact]
    public void Resync_WhenAGroupAlreadyMatches_KeepsItsWatermark()
    {
        // Arrange
        var source = new WorkProjectFaker().Generate();
        var project = new WorkProject(source, Created);

        // Act
        var changed = project.Resync(source, Later);

        // Assert
        changed.Should().BeFalse();
        project.Watermarks.Should().Be(new WorkProjectWatermarks(Created, Created));
    }

    [Fact]
    public void Resync_WhenOnlyTheKeyDiffers_StampsOnlyTheKeyGroup()
    {
        // Arrange
        var id = Guid.NewGuid();
        var project = new WorkProject(new WorkProjectFaker().WithId(id).WithKey(new ProjectKey("OLDKEY")).Generate(), Created);
        var source = new WorkProjectFaker().WithId(id).WithKey(new ProjectKey("NEWKEY")).WithName(project.Name).WithDescription(project.Description).Generate();

        // Act
        var changed = project.Resync(source, Later);

        // Assert
        changed.Should().BeTrue();
        project.Key.Value.Should().Be("NEWKEY");
        project.Watermarks.Should().Be(new WorkProjectWatermarks(Created, Later));
    }
}
