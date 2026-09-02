using FluentAssertions;
using NodaTime;
using NodaTime.Extensions;
using NodaTime.Testing;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain.Models;
using Wayd.ProductManagement.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.ProductManagement.Domain.Tests.Sut.Models;

public sealed class ReleaseTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly ReleaseFaker _faker;

    public ReleaseTests()
    {
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
        _faker = new ReleaseFaker();
    }

    #region Create

    [Fact]
    public void Create_WhenValid_Success()
    {
        // Arrange
        var productId = Guid.CreateVersion7();
        var initialStatus = StatusRefFactory.For(StatusCategory.Proposed);

        // Act
        var result = Release.Create(
            productId, "2026.07", "Summer Release", new LocalDate(2026, 7, 31), null,
            initialStatus, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ProductId.Should().Be(productId);
        result.Value.Version.Should().Be("2026.07");
        result.Value.Name.Should().Be("Summer Release");
        result.Value.TargetDate.Should().Be(new LocalDate(2026, 7, 31));
    }

    [Fact]
    public void Create_ShouldAllowNoProduct_ForAReleaseSpanningProductLines()
    {
        // Act
        var result = Release.Create(
            null, "2026.07", null, null, null,
            StatusRefFactory.For(StatusCategory.Proposed), EventActor.System, _dateTimeProvider.Now);

        // Assert
        // A release announcing work across the API, the client and the MCP server has no single owner,
        // so requiring one would force a misleading choice between them.
        result.IsSuccess.Should().BeTrue();
        result.Value.ProductId.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldStartEmpty()
    {
        // Act
        var sut = Release.Create(
            null, "2026.07", null, null, null,
            StatusRefFactory.For(StatusCategory.Proposed), EventActor.System, _dateTimeProvider.Now).Value;

        // Assert
        // Unlike a package, which must be assembled from at least one component: an announcement is
        // commonly drafted before anyone knows which versions will make it.
        sut.IsEmpty.Should().BeTrue();
        sut.Versions.Should().BeEmpty();
        sut.Packages.Should().BeEmpty();
    }

    [Fact]
    public void Create_ShouldRaiseReleasePlannedEvent_AfterPersistence()
    {
        // Arrange & Act
        var sut = Release.Create(
            null, "2026.07", null, null, null,
            StatusRefFactory.For(StatusCategory.Proposed), EventActor.System, _dateTimeProvider.Now).Value;

        // Assert
        sut.DomainEvents.Should().BeEmpty();
        sut.PostPersistenceActions.First()();

        var planned = sut.DomainEvents.OfType<ReleasePlannedEvent>().Single();
        planned.Version.Should().Be("2026.07");
    }

    #endregion Create

    #region CarryVersions

    [Fact]
    public void CarryVersions_ShouldReplaceTheSetWholesale()
    {
        // Arrange
        var sut = _faker.Generate();
        var first = Guid.CreateVersion7();
        var second = Guid.CreateVersion7();

        // Act
        sut.CarryVersions([first], [], EventActor.System, _dateTimeProvider.Now);
        var result = sut.CarryVersions([second], [], EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Whole-set replacement, not incremental: a version left out of the request is removed.
        result.IsSuccess.Should().BeTrue();
        sut.Versions.Select(v => v.VersionId).Should().Equal(second);
    }

    [Fact]
    public void CarryVersions_ShouldFail_WhenTheVersionAlreadyShipsInOneOfThePackages()
    {
        // Arrange
        var sut = _faker.Generate();
        var versionId = Guid.CreateVersion7();

        // Act
        var result = sut.CarryVersions([versionId], [versionId], EventActor.System, _dateTimeProvider.Now);

        // Assert
        // The double-count rule: a version shipped inside a package and also listed directly would be
        // announced twice by one release, making "what did 2026.07 contain" answerable two ways.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("cannot also be carried directly");
        sut.Versions.Should().BeEmpty();
    }

    [Fact]
    public void CarryVersions_ShouldFail_WhenTheSameVersionAppearsTwice()
    {
        // Arrange
        var sut = _faker.Generate();
        var versionId = Guid.CreateVersion7();

        // Act
        var result = sut.CarryVersions([versionId, versionId], [], EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A version can appear only once in a release.");
    }

    [Fact]
    public void CarryVersions_ShouldRaiseNothing_WhenTheSetIsUnchanged()
    {
        // Arrange
        var sut = _faker.Generate();
        var versionId = Guid.CreateVersion7();
        sut.CarryVersions([versionId], [], EventActor.System, _dateTimeProvider.Now);
        sut.ClearDomainEvents();

        // Act
        var result = sut.CarryVersions([versionId], [], EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void CarryVersions_ShouldFail_WhenTheReleaseHasBeenAnnounced()
    {
        // Arrange
        var sut = _faker.AsReleased(new LocalDate(2026, 7, 31)).Generate();

        // Act
        var result = sut.CarryVersions([Guid.CreateVersion7()], [], EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Once announced, the contents are the record of what shipped rather than a plan.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A released release's contents cannot be amended.");
    }

    [Fact]
    public void CarryVersions_ShouldFail_WhenWithdrawn()
    {
        // Arrange
        var sut = _faker.AsWithdrawn().Generate();

        // Act
        var result = sut.CarryVersions([Guid.CreateVersion7()], [], EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A withdrawn release's contents cannot be amended.");
    }

    #endregion CarryVersions

    #region ShipPackages

    [Fact]
    public void ShipPackages_ShouldReplaceTheSetWholesale()
    {
        // Arrange
        var sut = _faker.Generate();
        var packageId = Guid.CreateVersion7();

        // Act
        var result = sut.ShipPackages([packageId], [], EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Packages.Select(p => p.PackageId).Should().Equal(packageId);
    }

    [Fact]
    public void ShipPackages_ShouldFail_WhenAPackageShipsAVersionAlreadyCarriedDirectly()
    {
        // Arrange
        var sut = _faker.Generate();
        var versionId = Guid.CreateVersion7();
        sut.CarryVersions([versionId], [], EventActor.System, _dateTimeProvider.Now);

        // Act
        var result = sut.ShipPackages([Guid.CreateVersion7()], [versionId], EventActor.System, _dateTimeProvider.Now);

        // Assert
        // The same rule enforced from the other side: adding a package must not duplicate a version
        // the release already carries on its own.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already carries a version directly");
        sut.Packages.Should().BeEmpty();
    }

    [Fact]
    public void ShipPackages_ShouldFail_WhenTheSamePackageAppearsTwice()
    {
        // Arrange
        var sut = _faker.Generate();
        var packageId = Guid.CreateVersion7();

        // Act
        var result = sut.ShipPackages([packageId, packageId], [], EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A package can appear only once in a release.");
    }

    #endregion ShipPackages

    #region MarkReleased

    [Fact]
    public void MarkReleased_ShouldRecordTheDateAndRaiseEvent()
    {
        // Arrange
        var sut = _faker.Generate();
        var releasedDate = new LocalDate(2026, 7, 31);

        // Act
        var result = sut.MarkReleased(
            releasedDate, hasUnreleasedContents: false, StatusRefFactory.Released(),
            EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ReleasedDate.Should().Be(releasedDate);
        sut.StatusCategory.Should().Be(StatusCategory.Done);
        sut.DomainEvents.Should().ContainSingle(e => e is ReleaseReleasedEvent);
    }

    [Fact]
    public void MarkReleased_ShouldFail_WhenSomethingItCarriesHasNotShipped()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.MarkReleased(
            new LocalDate(2026, 7, 31), hasUnreleasedContents: true, StatusRefFactory.Released(),
            EventActor.System, _dateTimeProvider.Now);

        // Assert
        // The one claim a release can make that its own contents contradict: telling customers 2026.07
        // shipped while a version inside it has not.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("has not shipped");
        sut.ReleasedDate.Should().BeNull();
    }

    [Fact]
    public void MarkReleased_ShouldSucceed_WhenTheReleaseIsEmpty()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.MarkReleased(
            new LocalDate(2026, 7, 31), hasUnreleasedContents: false, StatusRefFactory.Released(),
            EventActor.System, _dateTimeProvider.Now);

        // Assert
        // An empty release is legitimate, not a draft: a repackaging or a pricing change is announced
        // with nothing deployed.
        result.IsSuccess.Should().BeTrue();
        sut.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void MarkReleased_ShouldFail_WhenAlreadyReleased()
    {
        // Arrange
        var sut = _faker.AsReleased(new LocalDate(2026, 7, 31)).Generate();

        // Act
        var result = sut.MarkReleased(
            new LocalDate(2026, 8, 1), hasUnreleasedContents: false, StatusRefFactory.Released(),
            EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This release has already been released.");
    }

    #endregion MarkReleased

    #region CorrectDates

    [Fact]
    public void CorrectDates_ShouldChangeDatesWithoutMovingStatus()
    {
        // Arrange
        var sut = _faker.AsReleased(new LocalDate(2026, 7, 31)).Generate();
        var statusBefore = sut.StatusId;

        // Act
        var result = sut.CorrectDates(
            new LocalDate(2026, 7, 1), new LocalDate(2026, 8, 2), EventActor.System, _dateTimeProvider.Now);

        // Assert
        // A correction says what was written down was wrong, not that the release moved.
        result.IsSuccess.Should().BeTrue();
        sut.TargetDate.Should().Be(new LocalDate(2026, 7, 1));
        sut.ReleasedDate.Should().Be(new LocalDate(2026, 8, 2));
        sut.StatusId.Should().Be(statusBefore);
        sut.StatusTransitions.Should().BeEmpty();
    }

    [Fact]
    public void CorrectDates_ShouldFail_WhenClearingTheReleasedDateOfAnAnnouncedRelease()
    {
        // Arrange
        var sut = _faker.AsReleased(new LocalDate(2026, 7, 31)).Generate();

        // Act
        var result = sut.CorrectDates(null, null, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Emptying it would leave the status contradicting the dates; reverting is the action for that.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Revert the release instead");
    }

    #endregion CorrectDates

    #region Withdraw and Revert

    [Fact]
    public void Withdraw_ShouldMoveToWithdrawnAndRaiseEvent()
    {
        // Arrange
        var sut = _faker.AsReleased(new LocalDate(2026, 7, 31)).Generate();

        // Act
        var result = sut.Withdraw(
            "Pricing error in the announcement.", StatusRefFactory.Withdrawn(),
            EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.StatusCategory.Should().Be(StatusCategory.Removed);

        var withdrawn = sut.DomainEvents.OfType<ReleaseWithdrawnEvent>().Single();
        withdrawn.Reason.Should().Be("Pricing error in the announcement.");
    }

    [Fact]
    public void Withdraw_ShouldKeepTheReleasedDate()
    {
        // Arrange
        var releasedDate = new LocalDate(2026, 7, 31);
        var sut = _faker.AsReleased(releasedDate).Generate();

        // Act
        sut.Withdraw(null, StatusRefFactory.Withdrawn(), EventActor.System, _dateTimeProvider.Now);

        // Assert
        // A retraction says a real announcement went out and was pulled, so the date it went out stands.
        sut.ReleasedDate.Should().Be(releasedDate);
    }

    [Fact]
    public void RevertRelease_ShouldClearTheReleasedDateAndRequireAReason()
    {
        // Arrange
        var sut = _faker.AsReleased(new LocalDate(2026, 7, 31)).Generate();

        // Act
        var result = sut.RevertRelease(
            StatusRefFactory.Ready(), "Announced against the wrong record.",
            EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Reverting says the announcement never happened, so the date goes with the status.
        result.IsSuccess.Should().BeTrue();
        sut.ReleasedDate.Should().BeNull();
        sut.DomainEvents.OfType<ReleaseRevertedEvent>().Single()
            .FromReleasedDate.Should().Be(new LocalDate(2026, 7, 31));
    }

    [Fact]
    public void RevertRelease_ShouldFail_WhenNoReasonIsGiven()
    {
        // Arrange
        var sut = _faker.AsReleased(new LocalDate(2026, 7, 31)).Generate();

        // Act
        var result = sut.RevertRelease(
            StatusRefFactory.Ready(), "   ", EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Unlike a withdrawal's optional reason: this contradicts what the history already asserts.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A reason is required to revert a release.");
    }

    [Fact]
    public void RevertRelease_ShouldFail_WhenNeverReleased()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.RevertRelease(
            StatusRefFactory.Ready(), "Nothing to undo.", EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This release has not been released, so there is nothing to revert.");
    }

    #endregion Withdraw and Revert

    #region UpdateDetails

    [Fact]
    public void UpdateDetails_ShouldChangeTheOwningProduct()
    {
        // Arrange
        var sut = _faker.WithProductId(Guid.CreateVersion7()).Generate();

        // Act
        var result = sut.UpdateDetails(
            sut.Version, sut.Name, sut.Notes, null, sut.Sequence, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Clearing the owner is how a release becomes one that spans product lines.
        result.IsSuccess.Should().BeTrue();
        sut.ProductId.Should().BeNull();
    }

    [Fact]
    public void UpdateDetails_ShouldRaiseNothing_WhenEveryValueAlreadyMatches()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.UpdateDetails(
            sut.Version, sut.Name, sut.Notes, sut.ProductId, sut.Sequence,
            EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion UpdateDetails
}
