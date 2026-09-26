using FluentAssertions;
using NodaTime;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.ReleasePackages.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.ReleasePackages.Commands;

/// <summary>
/// Correcting a package's recorded dates without moving its status.
/// </summary>
public sealed class CorrectReleasePackageDatesCommandHandlerTests : ProductCommandTestBase
{
    private CorrectReleasePackageDatesCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, CurrentPrincipal.Object, Logger<CorrectReleasePackageDatesCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldCorrectTheReleasedDate_WithoutMovingTheStatus()
    {
        // Arrange
        var product = SeedProduct();
        var package = SeedReleasePackage(product.Id);
        package.MarkReleased(
            new LocalDate(2026, 9, 18), Status("Released", StatusCategory.Done, ProductStatusAlias.Released), EventActor.System, Now);
        package.ClearDomainEvents();
        var statusBefore = package.StatusId;
        var transitionsBefore = package.StatusTransitions.Count;
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CorrectReleasePackageDatesCommand(package.Id, null, new LocalDate(2026, 9, 17)),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        package.ReleasedDate.Should().Be(new LocalDate(2026, 9, 17));
        package.StatusId.Should().Be(statusBefore);
        package.StatusTransitions.Should().HaveCount(transitionsBefore);
        package.DomainEvents.Should().ContainSingle(e => e is PackageDatesCorrectedEvent);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenAddingAReleasedDateToAnUnreleasedPackage()
    {
        // Arrange
        var product = SeedProduct();
        var package = SeedReleasePackage(product.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CorrectReleasePackageDatesCommand(package.Id, null, new LocalDate(2026, 9, 17)),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        package.ReleasedDate.Should().BeNull();
        package.DomainEvents.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenThePackageDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CorrectReleasePackageDatesCommand(Guid.CreateVersion7(), null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Release package not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
