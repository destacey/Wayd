using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Application.Versions.Commands;

namespace Wayd.ProductManagement.Application.Tests.Sut.Versions.Commands;

/// <summary>
/// Correcting a version's recorded target date and cut and released moments without moving its status.
/// </summary>
public sealed class CorrectVersionDatesCommandHandlerTests : ProductCommandTestBase
{
    private static readonly Instant CutAt = Instant.FromUtc(2026, 9, 17, 15, 0);
    private static readonly Instant ReleasedAt = Instant.FromUtc(2026, 9, 18, 2, 30);

    private CorrectVersionDatesCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, CurrentPrincipal.Object, Logger<CorrectVersionDatesCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldCorrectEveryValue_WithoutMovingTheStatus()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        version.Cut(CutAt, Status("Ready", StatusCategory.Active, ProductStatusAlias.Ready), product.Name, EventActor.System, Now);
        version.MarkReleased(
            ReleasedAt, Status("Released", StatusCategory.Done, ProductStatusAlias.Released), product.Name, EventActor.System, Now);
        version.ClearDomainEvents();
        var statusBefore = version.StatusId;
        var transitionsBefore = version.StatusTransitions.Count;
        var correctedCut = CutAt.Minus(Duration.FromHours(4));
        var correctedRelease = ReleasedAt.Minus(Duration.FromHours(1));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CorrectVersionDatesCommand(version.Id, new LocalDate(2026, 9, 20), correctedCut, correctedRelease),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        version.TargetDate.Should().Be(new LocalDate(2026, 9, 20));
        version.CutAt.Should().Be(correctedCut);
        version.ReleasedAt.Should().Be(correctedRelease);
        version.StatusId.Should().Be(statusBefore);
        version.StatusTransitions.Should().HaveCount(transitionsBefore);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRecordTheCorrectionWithBothEnds_AndWhoMadeIt()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        version.MarkReleased(
            ReleasedAt, Status("Released", StatusCategory.Done, ProductStatusAlias.Released), product.Name, EventActor.System, Now);
        version.ClearDomainEvents();
        var corrected = ReleasedAt.Plus(Duration.FromDays(1));
        var sut = CreateSut();

        // Act
        await sut.Handle(
            new CorrectVersionDatesCommand(version.Id, null, null, corrected), TestContext.Current.CancellationToken);

        // Assert
        var raised = version.DomainEvents.OfType<VersionDatesCorrectedEventV2>().Should().ContainSingle().Subject;
        raised.FromReleasedAt.Should().Be(ReleasedAt);
        raised.ToReleasedAt.Should().Be(corrected);
        raised.ProductName.Should().Be(product.Name);
        raised.Actor.EmployeeId.Should().Be(ActingEmployeeId);
    }

    [Fact]
    public async Task Handle_ShouldAddACutMoment_ToAVersionReleasedWithoutOne()
    {
        // Arrange — hand-entry and import both release without cutting; the cut is often found later
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        version.MarkReleased(
            ReleasedAt, Status("Released", StatusCategory.Done, ProductStatusAlias.Released), product.Name, EventActor.System, Now);
        version.ClearDomainEvents();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CorrectVersionDatesCommand(version.Id, null, CutAt, ReleasedAt), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        version.CutAt.Should().Be(CutAt);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenClearingTheReleasedMoment()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        version.MarkReleased(
            ReleasedAt, Status("Released", StatusCategory.Done, ProductStatusAlias.Released), product.Name, EventActor.System, Now);
        version.ClearDomainEvents();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CorrectVersionDatesCommand(version.Id, null, null, null), TestContext.Current.CancellationToken);

        // Assert — reverting is the action that says it did not ship
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Revert the version instead");
        version.ReleasedAt.Should().Be(ReleasedAt);
        version.DomainEvents.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenReleasedBeforeItWasCut()
    {
        // Arrange — the same evening, an hour apart, is enough to be refused
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CorrectVersionDatesCommand(version.Id, null, CutAt, CutAt.Minus(Duration.FromHours(1))),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A version cannot be released before it was cut.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenWithdrawn()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        version.Withdraw(null, Status("Withdrawn", StatusCategory.Removed, ProductStatusAlias.Withdrawn), product.Name, EventActor.System, Now);
        version.ClearDomainEvents();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CorrectVersionDatesCommand(version.Id, new LocalDate(2026, 9, 20), null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A withdrawn version cannot have its dates corrected.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheVersionDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CorrectVersionDatesCommand(Guid.CreateVersion7(), null, null, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Version not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
