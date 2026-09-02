using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.Versions.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;

// The delivery artifact record, not System.Version.
using Version = Wayd.ProductManagement.Domain.Models.Version;

namespace Wayd.ProductManagement.Application.Tests.Sut.Versions.Commands;

/// <summary>
/// Recording that a version shipped.
/// </summary>
public sealed class MarkVersionReleasedCommandHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly StatusRef _released = Status("Released", StatusCategory.Done, ProductStatusAlias.Released);

    public MarkVersionReleasedCommandHandlerTests()
    {
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.Version.Key, null, (int)ProductStatusAlias.Released, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(_released));
    }

    private MarkVersionReleasedCommandHandler CreateSut() =>
        new(DbContext, _statusResolver.Object, CurrentUser.Object, CurrentPrincipal.Object, Logger<MarkVersionReleasedCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldRecordTheShipment()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new MarkVersionReleasedCommand(version.Id, new LocalDate(2026, 6, 2)),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        version.ReleasedDate.Should().Be(new LocalDate(2026, 6, 2));
        version.StatusCategory.Should().Be(StatusCategory.Done);
    }

    [Fact]
    public async Task Handle_ShouldRefuseASecondShipment()
    {
        // Arrange
        var product = SeedProduct();
        var version = SeedVersion(product.Id);
        var sut = CreateSut();
        await sut.Handle(
            new MarkVersionReleasedCommand(version.Id, new LocalDate(2026, 6, 2)),
            TestContext.Current.CancellationToken);

        // Act
        var result = await sut.Handle(
            new MarkVersionReleasedCommand(version.Id, new LocalDate(2026, 7, 2)),
            TestContext.Current.CancellationToken);

        // Assert
        // The released date orders the delivery history, so overwriting it would rewrite the past.
        result.IsFailure.Should().BeTrue();
        version.ReleasedDate.Should().Be(new LocalDate(2026, 6, 2));
    }
}
