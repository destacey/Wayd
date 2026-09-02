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
    private const string ProductName = "Checkout";

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
        var result = Release.Create(productId, "4.8.2", "Autumn release", new LocalDate(2026, 9, 30), null, true, initialStatus, ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ProductId.Should().Be(productId);
        result.Value.Version.Should().Be("4.8.2");
        result.Value.Name.Should().Be("Autumn release");
        result.Value.TargetDate.Should().Be(new LocalDate(2026, 9, 30));
        result.Value.Sequence.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldFail_WhenTheProductTypeIsNotReleasable()
    {
        // Act
        var result = Release.Create(Guid.CreateVersion7(), "4.8.2", null, null, null, isProductReleasable: false, StatusRefFactory.For(StatusCategory.Proposed), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Releases cannot be cut against this product's type.");
    }

    [Theory]
    [InlineData("4.8.2")]
    [InlineData("2026.08")]
    [InlineData("v3-beta")]
    [InlineData("20260829.3")]
    [InlineData("release-candidate-1")]
    public void Create_ShouldAcceptAnyVersionString(string version)
    {
        // Act
        var result = Release.Create(Guid.CreateVersion7(), version, null, null, null, true, StatusRefFactory.For(StatusCategory.Proposed), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Version is free text and never parsed: Wayd observes releases rather than owning them, so any
        // scheme an organization uses has to survive unaltered.
        result.IsSuccess.Should().BeTrue();
        result.Value.Version.Should().Be(version);
    }

    [Fact]
    public void Create_ShouldRaiseReleasePlannedEvent_AfterPersistence()
    {
        // Arrange & Act
        var sut = Release.Create(Guid.CreateVersion7(), "4.8.2", null, null, null, true, StatusRefFactory.For(StatusCategory.Proposed), ProductName, EventActor.System, _dateTimeProvider.Now).Value;

        // Assert
        sut.DomainEvents.Should().BeEmpty();
        sut.PostPersistenceActions.First()();

        var planned = sut.DomainEvents.OfType<ReleasePlannedEvent>().Single();
        planned.Version.Should().Be("4.8.2");
        planned.ProductName.Should().Be(ProductName);
    }

    #endregion Create

    #region Sequence

    [Fact]
    public void Create_ShouldAcceptASequence_ForABackportThatShipsAfterItsSuccessor()
    {
        // Arrange
        // 4.7.5 ships after 5.0.0, so chronology alone would present it as the newest release.
        var sequence = 470L;

        // Act
        var result = Release.Create(Guid.CreateVersion7(), "4.7.5", null, null, sequence, true, StatusRefFactory.For(StatusCategory.Proposed), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Sequence.Should().Be(sequence);
    }

    [Fact]
    public void Create_ShouldLeaveSequenceNull_WhenChronologyIsSufficient()
    {
        // Act
        var result = Release.Create(Guid.CreateVersion7(), "5.0.0", null, null, null, true, StatusRefFactory.For(StatusCategory.Proposed), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Nullable and normally null: ordering comes from dates, which every source provides.
        result.Value.Sequence.Should().BeNull();
    }

    #endregion Sequence

    #region Cut

    [Fact]
    public void Cut_ShouldFreezeScopeAndMoveToReady()
    {
        // Arrange
        var sut = _faker.Generate();
        var ready = StatusRefFactory.Ready();
        var cutDate = new LocalDate(2026, 9, 1);

        // Act
        var result = sut.Cut(cutDate, ready, ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.CutDate.Should().Be(cutDate);
        sut.StatusId.Should().Be(ready.StatusId);
        sut.StatusCategory.Should().Be(StatusCategory.Active);
        sut.DomainEvents.Should().ContainSingle(e => e is ReleaseCutEvent);
    }

    [Fact]
    public void Cut_ShouldFail_WhenAlreadyCut()
    {
        // Arrange
        var sut = _faker.AsCut(new LocalDate(2026, 9, 1)).Generate();

        // Act
        var result = sut.Cut(new LocalDate(2026, 9, 2), StatusRefFactory.Ready(), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This release has already been cut.");
    }

    [Fact]
    public void Cut_ShouldFail_WhenWithdrawn()
    {
        // Arrange
        var sut = _faker.AsWithdrawn().Generate();

        // Act
        var result = sut.Cut(new LocalDate(2026, 9, 1), StatusRefFactory.Ready(), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A released or withdrawn release cannot be cut.");
    }

    #endregion Cut

    #region MarkReleased

    [Fact]
    public void MarkReleased_ShouldRecordTheShipDateAndRaiseEvent()
    {
        // Arrange
        var sut = _faker.AsCut(new LocalDate(2026, 9, 1)).Generate();
        var released = StatusRefFactory.Released();
        var releasedDate = new LocalDate(2026, 9, 5);

        // Act
        var result = sut.MarkReleased(releasedDate, released, ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ReleasedDate.Should().Be(releasedDate);
        sut.StatusCategory.Should().Be(StatusCategory.Done);
        sut.DomainEvents.Should().ContainSingle(e => e is ReleaseReleasedEvent);
    }

    [Fact]
    public void MarkReleased_ShouldFail_WhenReleasedBeforeItWasCut()
    {
        // Arrange
        var sut = _faker.AsCut(new LocalDate(2026, 9, 5)).Generate();

        // Act
        var result = sut.MarkReleased(new LocalDate(2026, 9, 1), StatusRefFactory.Released(), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The released date cannot be before the cut date.");
    }

    [Fact]
    public void MarkReleased_ShouldFail_WhenAlreadyReleased()
    {
        // Arrange
        var sut = _faker.AsReleased(new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 5)).Generate();

        // Act
        var result = sut.MarkReleased(new LocalDate(2026, 9, 6), StatusRefFactory.Released(), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This release has already been released.");
    }

    [Fact]
    public void MarkReleased_ShouldSucceed_WithoutHavingBeenCut()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.MarkReleased(new LocalDate(2026, 9, 5), StatusRefFactory.Released(), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Hand-entry and historical import land at release level with no cut recorded; refusing this
        // would make importing a year of past releases impossible.
        result.IsSuccess.Should().BeTrue();
        sut.CutDate.Should().BeNull();
    }

    #endregion MarkReleased

    #region CorrectDates

    [Fact]
    public void CorrectDates_ShouldReplaceBothDatesAndRaiseEvent()
    {
        // Arrange
        var sut = _faker.AsReleased(new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 5)).Generate();

        // Act
        var result = sut.CorrectDates(null, new LocalDate(2026, 9, 2), new LocalDate(2026, 9, 6), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.CutDate.Should().Be(new LocalDate(2026, 9, 2));
        sut.ReleasedDate.Should().Be(new LocalDate(2026, 9, 6));
        sut.DomainEvents.Should().ContainSingle(e => e is ReleaseDatesCorrectedEvent);
    }

    [Fact]
    public void CorrectDates_ShouldLeaveTheStatusWhereItIs()
    {
        // Arrange
        var sut = _faker.AsReleased(new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 5)).Generate();
        var statusId = sut.StatusId;

        // Act
        var result = sut.CorrectDates(null, new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 6), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // The point of the method: correcting a typo must not manufacture a status transition, which
        // is what withdrawing and re-releasing to fix a date would write into an append-only history.
        result.IsSuccess.Should().BeTrue();
        sut.StatusId.Should().Be(statusId);
        sut.StatusCategory.Should().Be(StatusCategory.Done);
        sut.DomainEvents.Should().NotContain(e => e is ReleaseReleasedEvent);
    }

    [Fact]
    public void CorrectDates_ShouldCarryBothEndsOfEachChange()
    {
        // Arrange
        var sut = _faker.AsReleased(new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 5)).Generate();

        // Act
        var result = sut.CorrectDates(null, new LocalDate(2026, 9, 2), new LocalDate(2026, 9, 6), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // The replaced value is the whole reason to record a correction; it is gone from the release.
        result.IsSuccess.Should().BeTrue();
        var raised = sut.DomainEvents.OfType<ReleaseDatesCorrectedEvent>().Single();
        raised.FromCutDate.Should().Be(new LocalDate(2026, 9, 1));
        raised.ToCutDate.Should().Be(new LocalDate(2026, 9, 2));
        raised.FromReleasedDate.Should().Be(new LocalDate(2026, 9, 5));
        raised.ToReleasedDate.Should().Be(new LocalDate(2026, 9, 6));
    }

    [Fact]
    public void CorrectDates_ShouldCorrectTheCutDate_WhenNotYetReleased()
    {
        // Arrange
        var sut = _faker.AsCut(new LocalDate(2026, 9, 1)).Generate();

        // Act
        var result = sut.CorrectDates(null, new LocalDate(2026, 9, 3), null, ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.CutDate.Should().Be(new LocalDate(2026, 9, 3));
        sut.ReleasedDate.Should().BeNull();
    }

    [Fact]
    public void CorrectDates_ShouldSucceedWithoutRaisingAnEvent_WhenNothingChanged()
    {
        // Arrange
        var sut = _faker.AsReleased(new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 5)).Generate();

        // Act
        var result = sut.CorrectDates(null, new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 5), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().NotContain(e => e is ReleaseDatesCorrectedEvent);
    }

    [Fact]
    public void CorrectDates_ShouldFail_WhenReleasedBeforeCut()
    {
        // Arrange
        var sut = _faker.AsReleased(new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 5)).Generate();

        // Act
        var result = sut.CorrectDates(null, new LocalDate(2026, 9, 6), new LocalDate(2026, 9, 5), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The released date cannot be before the cut date.");
        sut.CutDate.Should().Be(new LocalDate(2026, 9, 1));
    }

    [Fact]
    public void CorrectDates_ShouldAddACutDate_WhenTheReleaseWasNeverCut()
    {
        // Arrange — a release can be marked released without ever being cut, which historical import
        // depends on. A cut date discovered afterwards is a correction, not a lifecycle step.
        var sut = _faker.AsReleased(null, new LocalDate(2026, 9, 5)).Generate();

        // Act
        var result = sut.CorrectDates(null, new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 5), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.CutDate.Should().Be(new LocalDate(2026, 9, 1));
    }

    [Fact]
    public void CorrectDates_ShouldClearTheCutDate()
    {
        // Arrange — the cut date only records something written down, so removing a wrong one is a
        // correction like any other. The status is untouched either way.
        var sut = _faker.AsReleased(new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 5)).Generate();

        // Act
        var result = sut.CorrectDates(null, null, new LocalDate(2026, 9, 5), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.CutDate.Should().BeNull();
        sut.StatusCategory.Should().Be(StatusCategory.Done);
    }

    [Fact]
    public void CorrectDates_ShouldCorrectAndClearTheTargetDate()
    {
        // Arrange
        var sut = _faker.AsReleased(new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 5))
            .WithTargetDate(new LocalDate(2026, 8, 1))
            .Generate();

        // Act
        var corrected = sut.CorrectDates(new LocalDate(2026, 8, 15), new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 5), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert — a released release can still have a mis-typed target date fixed. MoveTargetDate
        // refuses once the release is Done, so without this there is no route to it at all.
        corrected.IsSuccess.Should().BeTrue();
        sut.TargetDate.Should().Be(new LocalDate(2026, 8, 15));

        // Act — and cleared, since a target date is only a statement of intent.
        var cleared = sut.CorrectDates(null, new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 5), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        cleared.IsSuccess.Should().BeTrue();
        sut.TargetDate.Should().BeNull();
    }

    [Fact]
    public void CorrectDates_ShouldFail_WhenClearingTheReleasedDate()
    {
        // Arrange
        var sut = _faker.AsReleased(new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 5)).Generate();

        // Act
        var result = sut.CorrectDates(null, new LocalDate(2026, 9, 1), null, ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert — the one date a correction cannot empty: the status would then contradict the
        // dates. Saying a release did not ship is RevertRelease's job, which moves the status too.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A released release cannot have its released date removed. Revert the release instead.");
        sut.ReleasedDate.Should().Be(new LocalDate(2026, 9, 5));
    }

    [Fact]
    public void CorrectDates_ShouldSucceed_WhenTheReleaseHasNoDatesYet()
    {
        // Arrange — a planned release with nothing recorded. Setting a target date on it is a
        // correction, not a lifecycle move, so there is nothing to refuse.
        var sut = _faker.Generate();

        // Act
        var result = sut.CorrectDates(new LocalDate(2026, 9, 20), null, null, ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.TargetDate.Should().Be(new LocalDate(2026, 9, 20));
    }

    [Fact]
    public void CorrectDates_ShouldFail_WhenWithdrawn()
    {
        // Arrange
        var sut = _faker.AsWithdrawn().Generate();

        // Act
        var result = sut.CorrectDates(null, new LocalDate(2026, 9, 2), null, ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A withdrawn release cannot have its dates corrected.");
    }

    #endregion CorrectDates

    #region Withdraw

    #region RevertRelease

    [Fact]
    public void RevertRelease_ShouldClearTheReleasedDateAndMoveBackToReady()
    {
        // Arrange
        var sut = _faker.AsReleased(new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 5)).Generate();

        // Act
        var result = sut.RevertRelease(StatusRefFactory.Ready(), "Marked released against the wrong version.", ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert — the released date and the status are one fact, so they move together.
        result.IsSuccess.Should().BeTrue();
        sut.ReleasedDate.Should().BeNull();
        sut.StatusCategory.Should().Be(StatusCategory.Active);
        sut.CutDate.Should().Be(new LocalDate(2026, 9, 1));
    }

    [Fact]
    public void RevertRelease_ShouldRaiseItsOwnEventRatherThanAWithdrawal()
    {
        // Arrange
        var sut = _faker.AsReleased(new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 5)).Generate();

        // Act
        var result = sut.RevertRelease(StatusRefFactory.Ready(), "Recorded in error.", ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert — a withdrawal asserts a real release was pulled. Reverting says it never shipped, so
        // a consumer counting releases must be able to tell the two apart.
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().NotContain(e => e is ReleaseWithdrawnEvent);

        var reverted = sut.DomainEvents.OfType<ReleaseRevertedEvent>().Single();
        reverted.Reason.Should().Be("Recorded in error.");
        reverted.FromReleasedDate.Should().Be(new LocalDate(2026, 9, 5));
    }

    [Fact]
    public void RevertRelease_ShouldFail_WhenTheReleaseWasNeverReleased()
    {
        // Arrange
        var sut = _faker.AsCut(new LocalDate(2026, 9, 1)).Generate();

        // Act
        var result = sut.RevertRelease(StatusRefFactory.Ready(), "Recorded in error.", ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This release has not been released, so there is nothing to revert.");
    }

    [Fact]
    public void RevertRelease_ShouldFail_WhenWithdrawn()
    {
        // Arrange — a release that shipped and was then pulled. The released date has to be present,
        // or the "nothing to revert" guard answers first and this asserts the wrong rule.
        var sut = _faker
            .AsReleased(new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 5))
            .AsWithdrawn()
            .Generate();

        // Act
        var result = sut.RevertRelease(StatusRefFactory.Ready(), "Recorded in error.", ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A withdrawn release cannot be reverted. Its status is already terminal.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RevertRelease_ShouldFail_WithoutAReason(string reason)
    {
        // Arrange
        var sut = _faker.AsReleased(new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 5)).Generate();

        // Act
        var result = sut.RevertRelease(StatusRefFactory.Ready(), reason, ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert — unlike a withdrawal's optional reason, this contradicts something the history
        // already asserts, so the record has to say why.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A reason is required to revert a release.");
        sut.ReleasedDate.Should().Be(new LocalDate(2026, 9, 5));
    }

    #endregion RevertRelease

    [Fact]
    public void Withdraw_ShouldMoveToRemovedAndRaiseEventWithReason()
    {
        // Arrange
        var sut = _faker.AsCut(new LocalDate(2026, 9, 1)).Generate();

        // Act
        var result = sut.Withdraw("Critical defect found in staging.", StatusRefFactory.Withdrawn(), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.StatusCategory.Should().Be(StatusCategory.Removed);

        var withdrawn = sut.DomainEvents.OfType<ReleaseWithdrawnEvent>().Single();
        withdrawn.Reason.Should().Be("Critical defect found in staging.");
        withdrawn.Version.Should().Be(sut.Version);
    }

    [Fact]
    public void Withdraw_ShouldFail_WhenAlreadyWithdrawn()
    {
        // Arrange
        var sut = _faker.AsWithdrawn().Generate();

        // Act
        var result = sut.Withdraw(null, StatusRefFactory.Withdrawn(), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This release has already been withdrawn.");
    }

    #endregion Withdraw

    #region MoveTargetDate

    [Fact]
    public void MoveTargetDate_ShouldRaiseEventCarryingBothEnds()
    {
        // Arrange
        var from = new LocalDate(2026, 9, 30);
        var to = new LocalDate(2026, 10, 14);
        var sut = _faker.WithTargetDate(from).Generate();

        // Act
        var result = sut.MoveTargetDate(to, ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();

        // "Slipped two weeks" cannot be recovered from the new value alone.
        var moved = sut.DomainEvents.OfType<ReleaseTargetDateMovedEvent>().Single();
        moved.FromTargetDate.Should().Be(from);
        moved.ToTargetDate.Should().Be(to);
    }

    [Fact]
    public void MoveTargetDate_ShouldFail_WhenTheReleaseHasShipped()
    {
        // Arrange
        var sut = _faker.AsReleased(new LocalDate(2026, 9, 1), new LocalDate(2026, 9, 5)).Generate();

        // Act
        var result = sut.MoveTargetDate(new LocalDate(2026, 10, 14), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A released or withdrawn release cannot have its target date moved.");
    }

    [Fact]
    public void MoveTargetDate_ToTheSameDate_ShouldSucceedWithoutRaisingAnEvent()
    {
        // Arrange
        var date = new LocalDate(2026, 9, 30);
        var sut = _faker.WithTargetDate(date).Generate();

        // Act
        var result = sut.MoveTargetDate(date, ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    #endregion MoveTargetDate

    #region UpdateDetails

    [Fact]
    public void UpdateDetails_ShouldUpdateVersionAndSequence()
    {
        // Arrange
        var sut = _faker.WithVersion("4.8.1").Generate();

        // Act
        var result = sut.UpdateDetails("4.8.2", "Autumn release", "Fixed the checkout defect.", 482L, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Version.Should().Be("4.8.2");
        sut.Sequence.Should().Be(482L);
        sut.DomainEvents.Should().ContainSingle(e => e is ReleaseDetailsUpdatedEvent);
    }

    [Fact]
    public void UpdateDetails_WithUnchangedValues_ShouldSucceedWithoutRaisingAnEvent()
    {
        // Arrange
        var sut = _faker.WithVersion("4.8.2").WithName("Autumn release").WithNotes("Notes.").WithSequence(482L).Generate();

        // Act
        var result = sut.UpdateDetails("4.8.2", "Autumn release", "Notes.", 482L, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateDetails_ShouldRaiseAnEvent_WhenOnlyTheSequenceChanges()
    {
        // Arrange
        var sut = _faker.WithVersion("4.7.5").WithSequence(null).Generate();

        // Act
        var result = sut.UpdateDetails("4.7.5", sut.Name, sut.Notes, 470L, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Setting a backport's sequence is a real change even though nothing else moved.
        result.IsSuccess.Should().BeTrue();
        sut.Sequence.Should().Be(470L);
        sut.DomainEvents.Should().ContainSingle(e => e is ReleaseDetailsUpdatedEvent);
    }

    #endregion UpdateDetails
}
