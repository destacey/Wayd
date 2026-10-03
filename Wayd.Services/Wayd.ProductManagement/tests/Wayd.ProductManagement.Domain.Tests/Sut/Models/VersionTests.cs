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

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Domain.Tests.Sut.Models;

public sealed class VersionTests
{
    private const string ProductName = "Checkout";

    private readonly TestingDateTimeProvider _dateTimeProvider;
    private readonly VersionFaker _faker;

    public VersionTests()
    {
        _dateTimeProvider = new(new FakeClock(DateTime.UtcNow.ToInstant()));
        _faker = new VersionFaker();
    }

    #region Create

    [Fact]
    public void Create_WhenValid_Success()
    {
        // Arrange
        var productId = Guid.CreateVersion7();
        var initialStatus = StatusRefFactory.For(StatusCategory.Proposed);

        // Act
        var result = Version.Create(productId, "4.8.2", "Autumn version", new LocalDate(2026, 9, 30), null, true, initialStatus, ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.ProductId.Should().Be(productId);
        result.Value.Number.Should().Be("4.8.2");
        result.Value.Name.Should().Be("Autumn version");
        result.Value.TargetDate.Should().Be(new LocalDate(2026, 9, 30));
        result.Value.Sequence.Should().BeNull();
    }

    [Fact]
    public void Create_ShouldFail_WhenTheProductTypeIsNotReleasable()
    {
        // Act
        var result = Version.Create(Guid.CreateVersion7(), "4.8.2", null, null, null, isProductReleasable: false, StatusRefFactory.For(StatusCategory.Proposed), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Versions cannot be cut against this product's type.");
    }

    [Theory]
    [InlineData("4.8.2")]
    [InlineData("2026.08")]
    [InlineData("v3-beta")]
    [InlineData("20260829.3")]
    [InlineData("version-candidate-1")]
    public void Create_ShouldAcceptAnyVersionString(string version)
    {
        // Act
        var result = Version.Create(Guid.CreateVersion7(), version, null, null, null, true, StatusRefFactory.For(StatusCategory.Proposed), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Version is free text and never parsed: Wayd observes versions rather than owning them, so any
        // scheme an organization uses has to survive unaltered.
        result.IsSuccess.Should().BeTrue();
        result.Value.Number.Should().Be(version);
    }

    [Fact]
    public void Create_ShouldRaiseVersionPlannedEvent_AfterPersistence()
    {
        // Arrange & Act
        var sut = Version.Create(Guid.CreateVersion7(), "4.8.2", null, null, null, true, StatusRefFactory.For(StatusCategory.Proposed), ProductName, EventActor.System, _dateTimeProvider.Now).Value;

        // Assert
        sut.DomainEvents.Should().BeEmpty();
        sut.PostPersistenceActions.First()();

        var planned = sut.DomainEvents.OfType<VersionPlannedEvent>().Single();
        planned.Number.Should().Be("4.8.2");
        planned.ProductName.Should().Be(ProductName);
    }

    [Fact]
    public void Create_ThenWalkedToShippedBeforeTheFirstSave_RecordsTheVersionAsPlanned()
    {
        // Arrange — what the version import does: plan the version, then walk it to the status its row
        // describes, all before one save
        var proposed = StatusRefFactory.For(StatusCategory.Proposed);
        var sut = Version.Create(Guid.CreateVersion7(), "4.8.2", null, null, null, true, proposed, ProductName, EventActor.System, _dateTimeProvider.Now).Value;

        // Act
        sut.Cut(Instant.FromUtc(2026, 9, 1, 12, 0), StatusRefFactory.Ready(), ProductName, EventActor.System, _dateTimeProvider.Now);
        sut.MarkReleased(Instant.FromUtc(2026, 9, 30, 12, 0), StatusRefFactory.Released(), ProductName, EventActor.System, _dateTimeProvider.Now);
        sut.ExecutePostPersistenceActions();

        // Assert
        var planned = sut.DomainEvents.OfType<VersionPlannedEvent>().Should().ContainSingle().Subject;
        planned.StatusId.Should().Be(proposed.StatusId, "each transition is its own event");
        planned.StatusCategory.Should().Be(StatusCategory.Proposed);
    }

    #endregion Create

    #region Sequence

    [Fact]
    public void Create_ShouldAcceptASequence_ForABackportThatShipsAfterItsSuccessor()
    {
        // Arrange
        // 4.7.5 ships after 5.0.0, so chronology alone would present it as the newest version.
        var sequence = 470L;

        // Act
        var result = Version.Create(Guid.CreateVersion7(), "4.7.5", null, null, sequence, true, StatusRefFactory.For(StatusCategory.Proposed), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Sequence.Should().Be(sequence);
    }

    [Fact]
    public void Create_ShouldLeaveSequenceNull_WhenChronologyIsSufficient()
    {
        // Act
        var result = Version.Create(Guid.CreateVersion7(), "5.0.0", null, null, null, true, StatusRefFactory.For(StatusCategory.Proposed), ProductName, EventActor.System, _dateTimeProvider.Now);

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
        var cutAt = Instant.FromUtc(2026, 9, 1, 14, 30);

        // Act
        var result = sut.Cut(cutAt, ready, ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.CutAt.Should().Be(cutAt);
        sut.StatusId.Should().Be(ready.StatusId);
        sut.StatusCategory.Should().Be(StatusCategory.Active);
        sut.DomainEvents.Should().ContainSingle(e => e is VersionCutEventV2);
    }

    [Fact]
    public void Cut_ShouldFail_WhenAlreadyCut()
    {
        // Arrange
        var sut = _faker.AsCut(Instant.FromUtc(2026, 9, 1, 12, 0)).Generate();

        // Act
        var result = sut.Cut(Instant.FromUtc(2026, 9, 2, 12, 0), StatusRefFactory.Ready(), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This version has already been cut.");
    }

    [Fact]
    public void Cut_ShouldFail_WhenWithdrawn()
    {
        // Arrange
        var sut = _faker.AsWithdrawn().Generate();

        // Act
        var result = sut.Cut(Instant.FromUtc(2026, 9, 1, 12, 0), StatusRefFactory.Ready(), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A released or withdrawn version cannot be cut.");
    }

    #endregion Cut

    #region MarkReleased

    [Fact]
    public void MarkReleased_ShouldRecordTheShipDateAndRaiseEvent()
    {
        // Arrange
        var sut = _faker.AsCut(Instant.FromUtc(2026, 9, 1, 12, 0)).Generate();
        var released = StatusRefFactory.Released();
        var releasedAt = Instant.FromUtc(2026, 9, 5, 2, 30);

        // Act
        var result = sut.MarkReleased(releasedAt, released, ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.ReleasedAt.Should().Be(releasedAt);
        sut.StatusCategory.Should().Be(StatusCategory.Done);
        sut.DomainEvents.Should().ContainSingle(e => e is VersionReleasedEventV2);
    }

    [Fact]
    public void MarkReleased_ShouldFail_WhenReleasedBeforeItWasCut()
    {
        // Arrange
        var sut = _faker.AsCut(Instant.FromUtc(2026, 9, 5, 12, 0)).Generate();

        // Act
        var result = sut.MarkReleased(Instant.FromUtc(2026, 9, 1, 12, 0), StatusRefFactory.Released(), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A version cannot be released before it was cut.");
    }

    [Fact]
    public void MarkReleased_ShouldFail_WhenAlreadyReleased()
    {
        // Arrange
        var sut = _faker.AsReleased(Instant.FromUtc(2026, 9, 1, 12, 0), Instant.FromUtc(2026, 9, 5, 12, 0)).Generate();

        // Act
        var result = sut.MarkReleased(Instant.FromUtc(2026, 9, 6, 12, 0), StatusRefFactory.Released(), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This version has already been released.");
    }

    [Fact]
    public void MarkReleased_ShouldSucceed_WithoutHavingBeenCut()
    {
        // Arrange
        var sut = _faker.Generate();

        // Act
        var result = sut.MarkReleased(Instant.FromUtc(2026, 9, 5, 12, 0), StatusRefFactory.Released(), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Hand-entry and historical import land at version level with no cut recorded; refusing this
        // would make importing a year of past versions impossible.
        result.IsSuccess.Should().BeTrue();
        sut.CutAt.Should().BeNull();
    }

    #endregion MarkReleased

    #region CorrectDates

    [Fact]
    public void CorrectDates_ShouldReplaceBothDatesAndRaiseEvent()
    {
        // Arrange
        var sut = _faker.AsReleased(Instant.FromUtc(2026, 9, 1, 12, 0), Instant.FromUtc(2026, 9, 5, 12, 0)).Generate();

        // Act
        var result = sut.CorrectDates(null, Instant.FromUtc(2026, 9, 2, 12, 0), Instant.FromUtc(2026, 9, 6, 12, 0), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.CutAt.Should().Be(Instant.FromUtc(2026, 9, 2, 12, 0));
        sut.ReleasedAt.Should().Be(Instant.FromUtc(2026, 9, 6, 12, 0));
        sut.DomainEvents.Should().ContainSingle(e => e is VersionDatesCorrectedEventV2);
    }

    [Fact]
    public void CorrectDates_ShouldLeaveTheStatusWhereItIs()
    {
        // Arrange
        var sut = _faker.AsReleased(Instant.FromUtc(2026, 9, 1, 12, 0), Instant.FromUtc(2026, 9, 5, 12, 0)).Generate();
        var statusId = sut.StatusId;

        // Act
        var result = sut.CorrectDates(null, Instant.FromUtc(2026, 9, 1, 12, 0), Instant.FromUtc(2026, 9, 6, 12, 0), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // The point of the method: correcting a typo must not manufacture a status transition, which
        // is what withdrawing and re-releasing to fix a date would write into an append-only history.
        result.IsSuccess.Should().BeTrue();
        sut.StatusId.Should().Be(statusId);
        sut.StatusCategory.Should().Be(StatusCategory.Done);
        sut.DomainEvents.Should().NotContain(e => e is VersionReleasedEventV2);
    }

    [Fact]
    public void CorrectDates_ShouldCarryBothEndsOfEachChange()
    {
        // Arrange
        var sut = _faker.AsReleased(Instant.FromUtc(2026, 9, 1, 12, 0), Instant.FromUtc(2026, 9, 5, 12, 0)).Generate();

        // Act
        var result = sut.CorrectDates(null, Instant.FromUtc(2026, 9, 2, 12, 0), Instant.FromUtc(2026, 9, 6, 12, 0), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // The replaced value is the whole reason to record a correction; it is gone from the version.
        result.IsSuccess.Should().BeTrue();
        var raised = sut.DomainEvents.OfType<VersionDatesCorrectedEventV2>().Single();
        raised.FromCutAt.Should().Be(Instant.FromUtc(2026, 9, 1, 12, 0));
        raised.ToCutAt.Should().Be(Instant.FromUtc(2026, 9, 2, 12, 0));
        raised.FromReleasedAt.Should().Be(Instant.FromUtc(2026, 9, 5, 12, 0));
        raised.ToReleasedAt.Should().Be(Instant.FromUtc(2026, 9, 6, 12, 0));
    }

    [Fact]
    public void CorrectDates_ShouldCorrectTheCutMoment_WhenNotYetReleased()
    {
        // Arrange
        var sut = _faker.AsCut(Instant.FromUtc(2026, 9, 1, 12, 0)).Generate();

        // Act
        var result = sut.CorrectDates(null, Instant.FromUtc(2026, 9, 3, 12, 0), null, ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.CutAt.Should().Be(Instant.FromUtc(2026, 9, 3, 12, 0));
        sut.ReleasedAt.Should().BeNull();
    }

    [Fact]
    public void CorrectDates_ShouldSucceedWithoutRaisingAnEvent_WhenNothingChanged()
    {
        // Arrange
        var sut = _faker.AsReleased(Instant.FromUtc(2026, 9, 1, 12, 0), Instant.FromUtc(2026, 9, 5, 12, 0)).Generate();

        // Act
        var result = sut.CorrectDates(null, Instant.FromUtc(2026, 9, 1, 12, 0), Instant.FromUtc(2026, 9, 5, 12, 0), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().NotContain(e => e is VersionDatesCorrectedEventV2);
    }

    [Fact]
    public void CorrectDates_ShouldFail_WhenReleasedBeforeCut()
    {
        // Arrange
        var sut = _faker.AsReleased(Instant.FromUtc(2026, 9, 1, 12, 0), Instant.FromUtc(2026, 9, 5, 12, 0)).Generate();

        // Act
        var result = sut.CorrectDates(null, Instant.FromUtc(2026, 9, 6, 12, 0), Instant.FromUtc(2026, 9, 5, 12, 0), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A version cannot be released before it was cut.");
        sut.CutAt.Should().Be(Instant.FromUtc(2026, 9, 1, 12, 0));
    }

    [Fact]
    public void CorrectDates_ShouldAddACutMoment_WhenTheReleaseWasNeverCut()
    {
        // Arrange — a version can be marked released without ever being cut, which historical import
        // depends on. A cut moment discovered afterwards is a correction, not a lifecycle step.
        var sut = _faker.AsReleased(null, Instant.FromUtc(2026, 9, 5, 12, 0)).Generate();

        // Act
        var result = sut.CorrectDates(null, Instant.FromUtc(2026, 9, 1, 12, 0), Instant.FromUtc(2026, 9, 5, 12, 0), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.CutAt.Should().Be(Instant.FromUtc(2026, 9, 1, 12, 0));
    }

    [Fact]
    public void CorrectDates_ShouldClearTheCutMoment()
    {
        // Arrange — the cut moment only records something written down, so removing a wrong one is a
        // correction like any other. The status is untouched either way.
        var sut = _faker.AsReleased(Instant.FromUtc(2026, 9, 1, 12, 0), Instant.FromUtc(2026, 9, 5, 12, 0)).Generate();

        // Act
        var result = sut.CorrectDates(null, null, Instant.FromUtc(2026, 9, 5, 12, 0), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.CutAt.Should().BeNull();
        sut.StatusCategory.Should().Be(StatusCategory.Done);
    }

    [Fact]
    public void CorrectDates_ShouldCorrectAndClearTheTargetDate()
    {
        // Arrange
        var sut = _faker.AsReleased(Instant.FromUtc(2026, 9, 1, 12, 0), Instant.FromUtc(2026, 9, 5, 12, 0))
            .WithTargetDate(new LocalDate(2026, 8, 1))
            .Generate();

        // Act
        var corrected = sut.CorrectDates(new LocalDate(2026, 8, 15), Instant.FromUtc(2026, 9, 1, 12, 0), Instant.FromUtc(2026, 9, 5, 12, 0), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert — a released version can still have a mis-typed target date fixed. MoveTargetDate
        // refuses once the version is Done, so without this there is no route to it at all.
        corrected.IsSuccess.Should().BeTrue();
        sut.TargetDate.Should().Be(new LocalDate(2026, 8, 15));

        // Act — and cleared, since a target date is only a statement of intent.
        var cleared = sut.CorrectDates(null, Instant.FromUtc(2026, 9, 1, 12, 0), Instant.FromUtc(2026, 9, 5, 12, 0), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        cleared.IsSuccess.Should().BeTrue();
        sut.TargetDate.Should().BeNull();
    }

    [Fact]
    public void CorrectDates_ShouldFail_WhenClearingTheReleasedMoment()
    {
        // Arrange
        var sut = _faker.AsReleased(Instant.FromUtc(2026, 9, 1, 12, 0), Instant.FromUtc(2026, 9, 5, 12, 0)).Generate();

        // Act
        var result = sut.CorrectDates(null, Instant.FromUtc(2026, 9, 1, 12, 0), null, ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert — the one date a correction cannot empty: the status would then contradict the
        // dates. Saying a version did not ship is RevertRelease's job, which moves the status too.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A released version cannot have its released moment removed. Send the released moment with the correction; revert the version only if it did not ship.");
        sut.ReleasedAt.Should().Be(Instant.FromUtc(2026, 9, 5, 12, 0));
    }

    [Fact]
    public void CorrectDates_ShouldSucceed_WhenTheReleaseHasNoDatesYet()
    {
        // Arrange — a planned version with nothing recorded. Setting a target date on it is a
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
        var result = sut.CorrectDates(null, Instant.FromUtc(2026, 9, 2, 12, 0), null, ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A withdrawn version cannot have its dates corrected.");
    }

    #endregion CorrectDates

    #region Withdraw

    #region RevertRelease

    [Fact]
    public void RevertRelease_ShouldClearTheReleasedMomentAndMoveBackToReady()
    {
        // Arrange
        var sut = _faker.AsReleased(Instant.FromUtc(2026, 9, 1, 12, 0), Instant.FromUtc(2026, 9, 5, 12, 0)).Generate();

        // Act
        var result = sut.RevertRelease(StatusRefFactory.Ready(), "Marked released against the wrong version.", ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert — the released moment and the status are one fact, so they move together.
        result.IsSuccess.Should().BeTrue();
        sut.ReleasedAt.Should().BeNull();
        sut.StatusCategory.Should().Be(StatusCategory.Active);
        sut.CutAt.Should().Be(Instant.FromUtc(2026, 9, 1, 12, 0));
    }

    [Fact]
    public void RevertRelease_ShouldRaiseItsOwnEventRatherThanAWithdrawal()
    {
        // Arrange
        var sut = _faker.AsReleased(Instant.FromUtc(2026, 9, 1, 12, 0), Instant.FromUtc(2026, 9, 5, 12, 0)).Generate();

        // Act
        var result = sut.RevertRelease(StatusRefFactory.Ready(), "Recorded in error.", ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert — a withdrawal asserts a real version was pulled. Reverting says it never shipped, so
        // a consumer counting versions must be able to tell the two apart.
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().NotContain(e => e is VersionWithdrawnEvent);

        var reverted = sut.DomainEvents.OfType<VersionRevertedEventV2>().Single();
        reverted.Reason.Should().Be("Recorded in error.");
        reverted.FromReleasedAt.Should().Be(Instant.FromUtc(2026, 9, 5, 12, 0));
    }

    [Fact]
    public void RevertRelease_ShouldFail_WhenTheReleaseWasNeverReleased()
    {
        // Arrange
        var sut = _faker.AsCut(Instant.FromUtc(2026, 9, 1, 12, 0)).Generate();

        // Act
        var result = sut.RevertRelease(StatusRefFactory.Ready(), "Recorded in error.", ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This version has not been released, so there is nothing to revert.");
    }

    [Fact]
    public void RevertRelease_ShouldFail_WhenWithdrawn()
    {
        // Arrange — a version that shipped and was then pulled. The released moment has to be present,
        // or the "nothing to revert" guard answers first and this asserts the wrong rule.
        var sut = _faker
            .AsReleased(Instant.FromUtc(2026, 9, 1, 12, 0), Instant.FromUtc(2026, 9, 5, 12, 0))
            .AsWithdrawn()
            .Generate();

        // Act
        var result = sut.RevertRelease(StatusRefFactory.Ready(), "Recorded in error.", ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A withdrawn version cannot be reverted. Its status is already terminal.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void RevertRelease_ShouldFail_WithoutAReason(string reason)
    {
        // Arrange
        var sut = _faker.AsReleased(Instant.FromUtc(2026, 9, 1, 12, 0), Instant.FromUtc(2026, 9, 5, 12, 0)).Generate();

        // Act
        var result = sut.RevertRelease(StatusRefFactory.Ready(), reason, ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert — unlike a withdrawal's optional reason, this contradicts something the history
        // already asserts, so the record has to say why.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A reason is required to revert a version.");
        sut.ReleasedAt.Should().Be(Instant.FromUtc(2026, 9, 5, 12, 0));
    }

    #endregion RevertRelease

    [Fact]
    public void Withdraw_ShouldMoveToRemovedAndRaiseEventWithReason()
    {
        // Arrange
        var sut = _faker.AsCut(Instant.FromUtc(2026, 9, 1, 12, 0)).Generate();

        // Act
        var result = sut.Withdraw("Critical defect found in staging.", StatusRefFactory.Withdrawn(), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.StatusCategory.Should().Be(StatusCategory.Removed);

        var withdrawn = sut.DomainEvents.OfType<VersionWithdrawnEvent>().Single();
        withdrawn.Reason.Should().Be("Critical defect found in staging.");
        withdrawn.Number.Should().Be(sut.Number);
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
        result.Error.Should().Be("This version has already been withdrawn.");
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
        var moved = sut.DomainEvents.OfType<VersionTargetDateMovedEvent>().Single();
        moved.FromTargetDate.Should().Be(from);
        moved.ToTargetDate.Should().Be(to);
    }

    [Fact]
    public void MoveTargetDate_ShouldFail_WhenTheVersionHasShipped()
    {
        // Arrange
        var sut = _faker.AsReleased(Instant.FromUtc(2026, 9, 1, 12, 0), Instant.FromUtc(2026, 9, 5, 12, 0)).Generate();

        // Act
        var result = sut.MoveTargetDate(new LocalDate(2026, 10, 14), ProductName, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A released or withdrawn version cannot have its target date moved.");
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
        var sut = _faker.WithNumber("4.8.1").Generate();

        // Act
        var result = sut.UpdateDetails("4.8.2", "Autumn version", "Fixed the checkout defect.", 482L, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.Number.Should().Be("4.8.2");
        sut.Sequence.Should().Be(482L);
        sut.DomainEvents.Should().ContainSingle(e => e is VersionDetailsUpdatedEvent);
    }

    [Fact]
    public void UpdateDetails_WithUnchangedValues_ShouldSucceedWithoutRaisingAnEvent()
    {
        // Arrange
        var sut = _faker.WithNumber("4.8.2").WithName("Autumn version").WithNotes("Notes.").WithSequence(482L).Generate();

        // Act
        var result = sut.UpdateDetails("4.8.2", "Autumn version", "Notes.", 482L, EventActor.System, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        sut.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateDetails_ShouldRaiseAnEvent_WhenOnlyTheSequenceChanges()
    {
        // Arrange
        var sut = _faker.WithNumber("4.7.5").WithSequence(null).Generate();

        // Act
        var result = sut.UpdateDetails("4.7.5", sut.Name, sut.Notes, 470L, EventActor.System, _dateTimeProvider.Now);

        // Assert
        // Setting a backport's sequence is a real change even though nothing else moved.
        result.IsSuccess.Should().BeTrue();
        sut.Sequence.Should().Be(470L);
        sut.DomainEvents.Should().ContainSingle(e => e is VersionDetailsUpdatedEvent);
    }

    #endregion UpdateDetails

    #region Delete

    [Fact]
    public void Delete_ShouldRaiseVersionDeletedEvent()
    {
        // Arrange
        var sut = _faker.Generate();
        sut.ClearDomainEvents();

        // Act
        sut.Delete(EventActor.System, _dateTimeProvider.Now);

        // Assert
        var deleted = sut.DomainEvents.OfType<VersionDeletedEvent>().Should().ContainSingle().Subject;
        deleted.Id.Should().Be(sut.Id);
        deleted.Key.Should().Be(sut.Key);
        deleted.ProductId.Should().Be(sut.ProductId);
        deleted.Number.Should().Be(sut.Number);
    }

    #endregion Delete
}
