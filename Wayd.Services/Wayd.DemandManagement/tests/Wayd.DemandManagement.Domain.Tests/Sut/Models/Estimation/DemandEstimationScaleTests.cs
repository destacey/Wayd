using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Events.Planning.EstimationScales;
using Wayd.DemandManagement.Domain.Models.Estimation;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Domain.Tests.Sut.Models.Estimation;

public sealed class DemandEstimationScaleTests
{
    private static readonly Instant AsOf = Instant.FromUtc(2026, 3, 1, 12, 0);

    [Fact]
    public void Constructor_CopiesTheSourceWithEveryVersionAndStampsEachGroup()
    {
        // Arrange
        var v1 = DemandEstimationScaleFactory.Version(1, [new("1", true), new("2", true)]);
        var v2 = DemandEstimationScaleFactory.Version(2, [new("1", true), new("2", true), new("?", false)]);
        var snapshot = DemandEstimationScaleFactory.Snapshot(Guid.NewGuid(), "Simple", isActive: false, [v1, v2], key: 12);

        // Act
        var sut = new DemandEstimationScale(snapshot, AsOf);

        // Assert
        sut.Id.Should().Be(snapshot.Id);
        sut.Key.Should().Be(12);
        sut.Name.Should().Be("Simple");
        sut.IsActive.Should().BeFalse();
        sut.Versions.Select(v => (v.Id, v.Number, v.ScaleId)).Should().BeEquivalentTo([(v1.VersionId, 1, snapshot.Id), (v2.VersionId, 2, snapshot.Id)]);
        sut.CurrentVersion.Id.Should().Be(v2.VersionId);
        sut.CurrentVersion.Values.Should().Equal(new DemandEstimationScaleValue("1", true), new("2", true), new("?", false));
        sut.Watermarks.Should().Be(new DemandEstimationScaleWatermarks(AsOf, AsOf));
    }

    [Fact]
    public void ApplyName_WhenNewer_RenamesAndAdvancesOnlyTheNameWatermark()
    {
        // Arrange
        var sut = DemandEstimationScaleFactory.Create(asOf: AsOf);
        var later = AsOf.Plus(Duration.FromMinutes(1));

        // Act
        var applied = sut.ApplyName("Sizes", later);

        // Assert
        applied.Should().BeTrue();
        sut.Name.Should().Be("Sizes");
        sut.Watermarks.Should().Be(new DemandEstimationScaleWatermarks(later, AsOf));
    }

    [Fact]
    public void ApplyName_WhenOlderThanTheLastRename_IsSkipped()
    {
        // Arrange
        var sut = DemandEstimationScaleFactory.Create(name: "Current", asOf: AsOf);

        // Act
        var applied = sut.ApplyName("Stale", AsOf.Minus(Duration.FromMinutes(1)));

        // Assert
        applied.Should().BeFalse();
        sut.Name.Should().Be("Current");
        sut.Watermarks.Name.Should().Be(AsOf);
    }

    [Fact]
    public void ApplyName_Redelivered_ReportsNoChange()
    {
        // Arrange
        var sut = DemandEstimationScaleFactory.Create(asOf: AsOf);
        var later = AsOf.Plus(Duration.FromMinutes(1));
        sut.ApplyName("Sizes", later);

        // Act
        var applied = sut.ApplyName("Sizes", later);

        // Assert
        applied.Should().BeFalse();
        sut.Name.Should().Be("Sizes");
    }

    [Fact]
    public void ApplyIsActive_WhenNewer_ChangesIt_AndAnOlderChangeIsThenSkipped()
    {
        // Arrange
        var sut = DemandEstimationScaleFactory.Create(isActive: true, asOf: AsOf);
        var deactivatedAt = AsOf.Plus(Duration.FromMinutes(2));

        // Act
        var deactivated = sut.ApplyIsActive(false, deactivatedAt);
        var staleActivation = sut.ApplyIsActive(true, AsOf.Plus(Duration.FromMinutes(1)));

        // Assert
        deactivated.Should().BeTrue();
        staleActivation.Should().BeFalse();
        sut.IsActive.Should().BeFalse();
        sut.Watermarks.Should().Be(new DemandEstimationScaleWatermarks(AsOf, deactivatedAt));
    }

    [Fact]
    public void ApplyIsActive_IsNotBlockedByANewerRename()
    {
        // Arrange
        var sut = DemandEstimationScaleFactory.Create(isActive: true, asOf: AsOf);
        sut.ApplyName("Renamed", AsOf.Plus(Duration.FromMinutes(5)));

        // Act
        var applied = sut.ApplyIsActive(false, AsOf.Plus(Duration.FromMinutes(1)));

        // Assert
        applied.Should().BeTrue();
        sut.IsActive.Should().BeFalse();
    }

    [Fact]
    public void AddVersion_AddsANewVersionWhichBecomesCurrent()
    {
        // Arrange
        var sut = DemandEstimationScaleFactory.Create(asOf: AsOf);
        var version = DemandEstimationScaleFactory.Version(2, [new("XS", true), new("S", true)]);

        // Act
        var added = sut.AddVersion(version);

        // Assert
        added.Should().BeTrue();
        sut.Versions.Should().HaveCount(2);
        sut.CurrentVersion.Id.Should().Be(version.VersionId);
        sut.CurrentVersion.Values.Select(v => v.Value).Should().Equal("XS", "S");
    }

    [Fact]
    public void AddVersion_Redelivered_ReportsNoChangeAndAddsNothing()
    {
        // Arrange
        var sut = DemandEstimationScaleFactory.Create(asOf: AsOf);
        var version = DemandEstimationScaleFactory.Version(2);
        sut.AddVersion(version);

        // Act
        var added = sut.AddVersion(version);

        // Assert
        added.Should().BeFalse();
        sut.Versions.Should().HaveCount(2);
    }

    [Fact]
    public void Resync_TakesTheSourcesNameStateAndMissingVersions()
    {
        // Arrange
        var sut = DemandEstimationScaleFactory.Create(name: "Old", isActive: true, asOf: AsOf);
        var existing = sut.CurrentVersion;
        var missing = DemandEstimationScaleFactory.Version(2);
        var readAt = AsOf.Plus(Duration.FromHours(1));
        var source = DemandEstimationScaleFactory.Snapshot(sut.Id, "New", isActive: false,
            [new EstimationScaleVersionValues(existing.Id, 1, DemandEstimationScaleFactory.TShirtSizes), missing]);

        // Act
        var changed = sut.Resync(source, readAt);

        // Assert
        changed.Should().BeTrue();
        sut.Name.Should().Be("New");
        sut.IsActive.Should().BeFalse();
        sut.Versions.Select(v => v.Id).Should().BeEquivalentTo([existing.Id, missing.VersionId]);
        sut.Watermarks.Should().Be(new DemandEstimationScaleWatermarks(readAt, readAt));
    }

    [Fact]
    public void Resync_LeavesAGroupHoldingANewerChange()
    {
        // Arrange
        var sut = DemandEstimationScaleFactory.Create(name: "Old", asOf: AsOf);
        var renamedAt = AsOf.Plus(Duration.FromHours(2));
        sut.ApplyName("Newest", renamedAt);
        var source = DemandEstimationScaleFactory.Snapshot(sut.Id, "Read earlier", isActive: true,
            [new EstimationScaleVersionValues(sut.CurrentVersion.Id, 1, DemandEstimationScaleFactory.TShirtSizes)]);

        // Act
        var changed = sut.Resync(source, AsOf.Plus(Duration.FromHours(1)));

        // Assert
        changed.Should().BeFalse();
        sut.Name.Should().Be("Newest");
        sut.Watermarks.Name.Should().Be(renamedAt);
    }

    [Fact]
    public void Resync_WithAnotherScale_Throws()
    {
        // Arrange
        var sut = DemandEstimationScaleFactory.Create(asOf: AsOf);
        var other = DemandEstimationScaleFactory.Snapshot(Guid.NewGuid(), "Other", true, [DemandEstimationScaleFactory.Version(1)]);

        // Act
        var act = () => sut.Resync(other, AsOf);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Theory]
    [InlineData("S", 0)]
    [InlineData(" XL ", 3)]
    public void FindEstimate_FindsAnEstimateValueWithItsPosition(string value, int position)
    {
        // Arrange
        var sut = DemandEstimationScaleFactory.Create().CurrentVersion;

        // Act
        var found = sut.FindEstimate(value);

        // Assert
        found.Should().NotBeNull();
        found!.Value.Value.Value.Should().Be(value.Trim());
        found.Value.Position.Should().Be(position);
    }

    [Theory]
    [InlineData("?")]
    [InlineData("s")]
    [InlineData("XXL")]
    [InlineData(" ")]
    [InlineData(null)]
    public void FindEstimate_FindsNothingForACardAnUnknownValueOrBlank(string? value)
    {
        // Arrange
        var sut = DemandEstimationScaleFactory.Create().CurrentVersion;

        // Act
        var found = sut.FindEstimate(value);

        // Assert
        found.Should().BeNull();
    }
}
