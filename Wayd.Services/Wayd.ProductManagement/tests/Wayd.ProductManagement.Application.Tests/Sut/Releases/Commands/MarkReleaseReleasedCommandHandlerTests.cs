using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Releases.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Tests.Sut.Releases.Commands;

/// <summary>
/// Announcing a release.
/// </summary>
/// <remarks>
/// The handler answers whether anything the release carries has yet to ship, because the aggregate
/// holds ids rather than the records. That is the one claim an announcement makes which its own
/// contents can contradict, so it is checked rather than assumed.
/// </remarks>
public sealed class MarkReleaseReleasedCommandHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();

    public MarkReleaseReleasedCommandHandlerTests()
    {
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Release.Key, null, (int)ProductStatusAlias.Released, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(
                Status("Released", StatusCategory.Done, ProductStatusAlias.Released)));
    }

    private MarkReleaseReleasedCommandHandler CreateSut() =>
        new(DbContext, _statusResolver.Object, CurrentUser.Object, CurrentPrincipal.Object,
            Logger<MarkReleaseReleasedCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldAnnounceAnEmptyRelease()
    {
        // Arrange
        var release = SeedRelease();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new MarkReleaseReleasedCommand(release.Id, new LocalDate(2026, 7, 31)),
            TestContext.Current.CancellationToken);

        // Assert
        // A repackaging or a pricing change is announced with nothing deployed.
        result.IsSuccess.Should().BeTrue();
        release.ReleasedDate.Should().Be(new LocalDate(2026, 7, 31));
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenAVersionItCarriesHasNotShipped()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        var release = SeedRelease();

        await new SetReleaseContentsCommandHandler(
                DbContext, CurrentUser.Object, CurrentPrincipal.Object,
                Logger<SetReleaseContentsCommandHandler>(), DateTimeProvider.Object)
            .Handle(new SetReleaseContentsCommand(release.Id, [version.Id], []), TestContext.Current.CancellationToken);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new MarkReleaseReleasedCommand(release.Id, new LocalDate(2026, 7, 31)),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("has not shipped");
        release.ReleasedDate.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldAnnounce_WhenEveryVersionItCarriesHasShipped()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        version.MarkReleased(
            new LocalDate(2026, 7, 20),
            Status("Released", StatusCategory.Done, ProductStatusAlias.Released),
            product.Name,
            Wayd.Common.Domain.Events.EventActor.System,
            Now);

        var release = SeedRelease();
        await new SetReleaseContentsCommandHandler(
                DbContext, CurrentUser.Object, CurrentPrincipal.Object,
                Logger<SetReleaseContentsCommandHandler>(), DateTimeProvider.Object)
            .Handle(new SetReleaseContentsCommand(release.Id, [version.Id], []), TestContext.Current.CancellationToken);

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new MarkReleaseReleasedCommand(release.Id, new LocalDate(2026, 7, 31)),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        release.ReleasedDate.Should().Be(new LocalDate(2026, 7, 31));
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheReleaseDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new MarkReleaseReleasedCommand(Guid.CreateVersion7(), new LocalDate(2026, 7, 31)),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Release not found.");
    }
}
