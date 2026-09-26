using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Releases.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Releases.Commands;

/// <summary>
/// Correcting a release's recorded target and announcement dates without moving its status.
/// </summary>
public sealed class CorrectReleaseDatesCommandHandlerTests : ProductCommandTestBase
{
    private static readonly LocalDate Announced = new(2026, 9, 18);

    private CorrectReleaseDatesCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, CurrentPrincipal.Object, Logger<CorrectReleaseDatesCommandHandler>(), DateTimeProvider.Object);

    private static StatusRef Released() => Status("Released", StatusCategory.Done, ProductStatusAlias.Released);

    [Fact]
    public async Task Handle_ShouldCorrectBothDates_WithoutMovingTheStatus()
    {
        // Arrange
        var release = SeedRelease();
        release.MarkReleased(Announced, hasUnreleasedContents: false, Released(), EventActor.System, Now);
        release.ClearDomainEvents();
        var statusBefore = release.StatusId;
        var transitionsBefore = release.StatusTransitions.Count;
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CorrectReleaseDatesCommand(release.Id, new LocalDate(2026, 9, 15), new LocalDate(2026, 9, 17)),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        release.TargetDate.Should().Be(new LocalDate(2026, 9, 15));
        release.ReleasedDate.Should().Be(new LocalDate(2026, 9, 17));
        release.StatusId.Should().Be(statusBefore);
        release.StatusTransitions.Should().HaveCount(transitionsBefore);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRecordTheCorrectionWithBothEnds_AndWhoMadeIt()
    {
        // Arrange
        var release = SeedRelease();
        release.MarkReleased(Announced, hasUnreleasedContents: false, Released(), EventActor.System, Now);
        release.ClearDomainEvents();
        var sut = CreateSut();

        // Act
        await sut.Handle(
            new CorrectReleaseDatesCommand(release.Id, null, new LocalDate(2026, 9, 17)),
            TestContext.Current.CancellationToken);

        // Assert
        var raised = release.DomainEvents.OfType<ReleaseDatesCorrectedEvent>().Should().ContainSingle().Subject;
        raised.FromReleasedDate.Should().Be(Announced);
        raised.ToReleasedDate.Should().Be(new LocalDate(2026, 9, 17));
        raised.Actor.EmployeeId.Should().Be(ActingEmployeeId);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenClearingTheReleasedDate()
    {
        // Arrange
        var release = SeedRelease();
        release.MarkReleased(Announced, hasUnreleasedContents: false, Released(), EventActor.System, Now);
        release.ClearDomainEvents();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CorrectReleaseDatesCommand(release.Id, null, null), TestContext.Current.CancellationToken);

        // Assert — an announced release with no announcement date contradicts its own status
        result.IsFailure.Should().BeTrue();
        release.ReleasedDate.Should().Be(Announced);
        release.DomainEvents.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenWithdrawn()
    {
        // Arrange
        var release = SeedRelease();
        release.Withdraw(null, Status("Withdrawn", StatusCategory.Removed, ProductStatusAlias.Withdrawn), EventActor.System, Now);
        release.ClearDomainEvents();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CorrectReleaseDatesCommand(release.Id, new LocalDate(2026, 9, 20), null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheReleaseDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CorrectReleaseDatesCommand(Guid.CreateVersion7(), null, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Release not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
