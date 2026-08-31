using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Releases.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Tests.Sut.Releases.Commands;

/// <summary>
/// Recording that a release shipped.
/// </summary>
public sealed class MarkReleaseReleasedCommandHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly StatusRef _released = Status("Released", StatusCategory.Done, ProductStatusAlias.Released);

    public MarkReleaseReleasedCommandHandlerTests()
    {
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Release.Key, null, (int)ProductStatusAlias.Released, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(_released));
    }

    private MarkReleaseReleasedCommandHandler CreateSut() =>
        new(DbContext, _statusResolver.Object, CurrentUser.Object, CurrentPrincipal.Object, Logger<MarkReleaseReleasedCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldRecordTheShipment()
    {
        // Arrange
        var product = SeedProduct();
        var release = SeedRelease(product.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new MarkReleaseReleasedCommand(release.Id, new LocalDate(2026, 6, 2)),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        release.ReleasedDate.Should().Be(new LocalDate(2026, 6, 2));
        release.StatusCategory.Should().Be(StatusCategory.Done);
    }

    [Fact]
    public async Task Handle_ShouldRefuseASecondShipment()
    {
        // Arrange
        var product = SeedProduct();
        var release = SeedRelease(product.Id);
        var sut = CreateSut();
        await sut.Handle(
            new MarkReleaseReleasedCommand(release.Id, new LocalDate(2026, 6, 2)),
            TestContext.Current.CancellationToken);

        // Act
        var result = await sut.Handle(
            new MarkReleaseReleasedCommand(release.Id, new LocalDate(2026, 7, 2)),
            TestContext.Current.CancellationToken);

        // Assert
        // The released date orders the delivery history, so overwriting it would rewrite the past.
        result.IsFailure.Should().BeTrue();
        release.ReleasedDate.Should().Be(new LocalDate(2026, 6, 2));
    }
}
