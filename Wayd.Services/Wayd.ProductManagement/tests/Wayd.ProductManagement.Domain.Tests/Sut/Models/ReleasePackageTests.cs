using FluentAssertions;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProductManagement.Domain.Tests.Sut.Models;

public sealed class ReleasePackageTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly ReleasePackageFaker _faker;

    public ReleasePackageTests()
    {
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
        _faker = new ReleasePackageFaker();
    }

    private static List<(Guid ProductId, Guid? ReleaseId, string Version, ManifestEntryKind Kind)> Manifest(int changed, int carriedForward)
    {
        var components = new List<(Guid, Guid?, string, ManifestEntryKind)>();

        for (var i = 0; i < changed; i++)
        {
            components.Add((Guid.CreateVersion7(), Guid.CreateVersion7(), $"1.{i}.0", ManifestEntryKind.Changed));
        }

        for (var i = 0; i < carriedForward; i++)
        {
            components.Add((Guid.CreateVersion7(), null, $"0.9.{i}", ManifestEntryKind.CarriedForward));
        }

        return components;
    }

    #region Create

    [Fact]
    public void Create_ShouldRecordChangedAndCarriedForwardComponentsAlike()
    {
        // Arrange
        var components = Manifest(changed: 4, carriedForward: 11);

        // Act
        var result = ReleasePackage.Create("2026.35", "Week 35", null, components, StatusRefFactory.For(StatusCategory.Proposed), EventActor.System, _dateTimeProvider.Now);

        // Assert
        // A weekly package where four of fifteen services changed still has to state what the other
        // eleven were running, or "what was in production on this date" is unanswerable.
        result.IsSuccess.Should().BeTrue();
        result.Value.Components.Should().HaveCount(15);
        result.Value.ChangedComponents.Should().HaveCount(4);
    }

    [Fact]
    public void Create_ShouldFail_WhenAComponentAppearsTwice()
    {
        // Arrange
        var productId = Guid.CreateVersion7();
        var components = new List<(Guid, Guid?, string, ManifestEntryKind)>
        {
            (productId, null, "1.0.0", ManifestEntryKind.Changed),
            (productId, null, "1.0.1", ManifestEntryKind.Changed)
        };

        // Act
        var result = ReleasePackage.Create("2026.35", null, null, components, StatusRefFactory.For(StatusCategory.Proposed), EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A component can appear only once in a package manifest.");
    }

    [Fact]
    public void Create_ShouldFail_WhenNoComponentsAreSupplied()
    {
        // Act
        var result = ReleasePackage.Create("2026.35", null, null, [], StatusRefFactory.For(StatusCategory.Proposed), EventActor.System, _dateTimeProvider.Now);

        // Assert
        // A package with nothing in it records nothing; permitting one also makes "has this been
        // assembled yet" impossible to answer from the manifest.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A package must be assembled from at least one component.");
    }

    [Fact]
    public void Create_ShouldRaisePackageAssembledEvent_AfterPersistence()
    {
        // Arrange
        var components = Manifest(changed: 2, carriedForward: 3);

        // Act
        var sut = ReleasePackage.Create("2026.35", null, null, components, StatusRefFactory.For(StatusCategory.Proposed), EventActor.System, _dateTimeProvider.Now).Value;

        // Assert
        sut.DomainEvents.Should().BeEmpty();
        sut.PostPersistenceActions.First()();

        var assembled = sut.DomainEvents.OfType<PackageAssembledEvent>().Single();
        assembled.ComponentCount.Should().Be(5);
        assembled.ChangedCount.Should().Be(2);
    }

    #endregion Create

    #region SetManifest

    [Fact]
    public void SetManifest_ShouldRaiseAmendedEvent_WhenReplacingAnExistingManifest()
    {
        // Arrange
        var existing = Manifest(changed: 1, carriedForward: 1)
            .Select(c => new ReleasePackageComponent(Guid.CreateVersion7(), c.ProductId, c.ReleaseId, c.Version, c.Kind));
        var sut = _faker.WithComponents(existing).Generate();

        // Act
        var result = sut.SetManifest(Manifest(changed: 3, carriedForward: 2), EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Amending after assembly means an earlier answer to "what was running" was wrong, which is a
        // different fact from the package changing state.
        result.IsSuccess.Should().BeTrue();
        sut.Components.Should().HaveCount(5);
        sut.DomainEvents.Should().ContainSingle(e => e is PackageManifestAmendedEvent);
    }

    [Fact]
    public void SetManifest_WithAnIdenticalManifest_ShouldNotRaiseAnAmendedEvent()
    {
        // Arrange
        var components = Manifest(changed: 2, carriedForward: 3);
        var existing = components.Select(c => new ReleasePackageComponent(Guid.CreateVersion7(), c.ProductId, c.ReleaseId, c.Version, c.Kind));
        var sut = _faker.WithComponents(existing).Generate();

        // Act
        var result = sut.SetManifest(components, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Claiming an amendment when the record of what shipped is unchanged reports the one thing this
        // event exists to say, falsely.
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void SetManifest_ShouldRaiseAmendedEvent_WhenOnlyAComponentVersionChanges()
    {
        // Arrange
        var components = Manifest(changed: 2, carriedForward: 1);
        var existing = components.Select(c => new ReleasePackageComponent(Guid.CreateVersion7(), c.ProductId, c.ReleaseId, c.Version, c.Kind));
        var sut = _faker.WithComponents(existing).Generate();

        var amended = components.ToList();
        amended[0] = (amended[0].ProductId, amended[0].ReleaseId, "9.9.9", amended[0].Kind);

        // Act
        var result = sut.SetManifest(amended, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().ContainSingle(e => e is PackageManifestAmendedEvent);
    }

    [Fact]
    public void SetManifest_ShouldFail_WhenTheReplacementIsEmpty()
    {
        // Arrange
        var existing = Manifest(changed: 1, carriedForward: 1)
            .Select(c => new ReleasePackageComponent(Guid.CreateVersion7(), c.ProductId, c.ReleaseId, c.Version, c.Kind));
        var sut = _faker.WithComponents(existing).Generate();

        // Act
        var result = sut.SetManifest([], EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Emptying a manifest would destroy the record that answers "what was running on this date"
        // while leaving the package otherwise intact.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A package manifest cannot be empty.");
        sut.Components.Should().HaveCount(2);
    }

    [Fact]
    public void SetManifest_ShouldFail_WhenThePackageIsWithdrawn()
    {
        // Arrange
        var sut = _faker.AsWithdrawn().Generate();

        // Act
        var result = sut.SetManifest(Manifest(changed: 1, carriedForward: 0), EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A withdrawn package's manifest cannot be amended.");
    }

    [Fact]
    public void SetManifest_ShouldFail_WhenThePackageHasShipped()
    {
        // Once shipped the manifest is the record of what went out. Rewriting it would claim a set of
        // versions that never shipped together.
        // Arrange
        var components = Manifest(changed: 2, carriedForward: 1)
            .Select(c => new ReleasePackageComponent(Guid.CreateVersion7(), c.ProductId, c.ReleaseId, c.Version, c.Kind));
        var sut = _faker.WithComponents(components).Generate();
        sut.MarkReleased(new LocalDate(2026, 8, 28), StatusRefFactory.Released(), EventActor.System, _dateTimeProvider.Now);

        // Act
        var result = sut.SetManifest(Manifest(changed: 1, carriedForward: 0), EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A released package's manifest cannot be amended.");
    }

    #endregion SetManifest

    #region MarkReleased

    [Fact]
    public void MarkReleased_ShouldRecordTheShipDate()
    {
        // Arrange
        var components = Manifest(changed: 2, carriedForward: 1)
            .Select(c => new ReleasePackageComponent(Guid.CreateVersion7(), c.ProductId, c.ReleaseId, c.Version, c.Kind));
        var sut = _faker.WithComponents(components).Generate();
        var releasedDate = new LocalDate(2026, 8, 28);

        // Act
        var result = sut.MarkReleased(releasedDate, StatusRefFactory.Released(), EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ReleasedDate.Should().Be(releasedDate);
        sut.DomainEvents.Should().ContainSingle(e => e is PackageReleasedEvent);
    }

    [Fact]
    public void MarkReleased_ShouldFail_WhenTheManifestIsEmpty()
    {
        // Arrange
        // Create and SetManifest both refuse an empty manifest, so this state is only reachable through
        // the faker. The guard stays as a backstop for rows loaded from the database.
        var sut = _faker.Generate();

        // Act
        var result = sut.MarkReleased(new LocalDate(2026, 8, 28), StatusRefFactory.Released(), EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A package cannot be released with an empty manifest.");
    }

    #endregion MarkReleased

    #region Withdraw

    [Fact]
    public void Withdraw_ShouldMoveToRemovedAndRaiseEvent()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.Withdraw("Coordinated rollback.", StatusRefFactory.Withdrawn(), EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.StatusCategory.Should().Be(StatusCategory.Removed);
        sut.DomainEvents.OfType<PackageWithdrawnEvent>().Single().Reason.Should().Be("Coordinated rollback.");
    }

    #endregion Withdraw
}
