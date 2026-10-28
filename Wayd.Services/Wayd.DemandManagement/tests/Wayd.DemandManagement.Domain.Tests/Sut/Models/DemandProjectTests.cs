using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Models.ProjectPortfolioManagement;
using Wayd.DemandManagement.Domain.Models;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Domain.Tests.Sut.Models;

public sealed class DemandProjectTests
{
    private static readonly Instant Created = Instant.FromUtc(2026, 1, 15, 9, 0, 0);
    private static readonly Instant Earlier = Created.Minus(Duration.FromMinutes(1));
    private static readonly Instant Later = Created.Plus(Duration.FromMinutes(1));

    [Fact]
    public void Constructor_CopiesTheSourceAndStampsEveryGroup()
    {
        // Arrange
        var source = SourceProject.Any("ATLAS", "Atlas");

        // Act
        var sut = new DemandProject(source, Created);

        // Assert
        sut.Id.Should().Be(source.Id);
        sut.Key.Value.Should().Be("ATLAS");
        sut.Name.Should().Be("Atlas");
        sut.Watermarks.Should().Be(DemandProjectWatermarks.At(Created));
    }

    [Fact]
    public void ApplyName_WhenNewer_RenamesWithoutTouchingTheKey()
    {
        // Arrange
        var sut = new DemandProject(SourceProject.Any("ATLAS", "Atlas"), Created);

        // Act
        var applied = sut.ApplyName("Borealis", Later);

        // Assert
        applied.Should().BeTrue();
        sut.Name.Should().Be("Borealis");
        sut.Key.Value.Should().Be("ATLAS");
        sut.Watermarks.Should().Be(new DemandProjectWatermarks(Later, Created));
    }

    [Fact]
    public void ApplyName_WhenOlderThanTheLastChange_IsSkipped()
    {
        // Arrange
        var sut = new DemandProject(SourceProject.Any(name: "Atlas"), Created);

        // Act
        var applied = sut.ApplyName("Borealis", Earlier);

        // Assert
        applied.Should().BeFalse();
        sut.Name.Should().Be("Atlas");
    }

    [Fact]
    public void ApplyName_WhenRedelivered_HasNothingToApply()
    {
        // Arrange
        var sut = new DemandProject(SourceProject.Any(), Created);
        sut.ApplyName("Borealis", Later);

        // Act
        var applied = sut.ApplyName("Borealis", Later);

        // Assert
        applied.Should().BeFalse();
    }

    [Fact]
    public void ApplyKey_WhenOlderThanTheLastChange_IsSkipped()
    {
        // Arrange
        var sut = new DemandProject(SourceProject.Any("FIRST"), Created);
        sut.ApplyKey(new ProjectKey("THIRD"), Later);

        // Act
        var applied = sut.ApplyKey(new ProjectKey("SECOND"), Created);

        // Assert
        applied.Should().BeFalse();
        sut.Key.Value.Should().Be("THIRD");
    }

    [Fact]
    public void Resync_WhenOnlyTheKeyDiffers_StampsOnlyTheKeyGroup()
    {
        // Arrange
        var source = SourceProject.Any("OLDKEY", "Atlas");
        var sut = new DemandProject(source, Created);

        // Act
        var changed = sut.Resync(source with { Key = new ProjectKey("NEWKEY") }, Later);

        // Assert
        changed.Should().BeTrue();
        sut.Key.Value.Should().Be("NEWKEY");
        sut.Watermarks.Should().Be(new DemandProjectWatermarks(Created, Later));
    }

    [Fact]
    public void Resync_WhenTheCopyHoldsANewerChange_KeepsIt()
    {
        // Arrange — renamed at Later; the bulk read was taken at Created, before the rename.
        var source = SourceProject.Any(name: "Atlas");
        var sut = new DemandProject(source, Earlier);
        sut.ApplyName("Borealis", Later);

        // Act
        var changed = sut.Resync(source, Created);

        // Assert
        changed.Should().BeFalse();
        sut.Name.Should().Be("Borealis");
    }

    [Fact]
    public void Resync_ForAnotherProject_Throws()
    {
        // Arrange
        var sut = new DemandProject(SourceProject.Any(), Created);

        // Act
        Action act = () => sut.Resync(SourceProject.Any(), Later);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }
}
