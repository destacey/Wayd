using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.ReleasePackages.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Tests.Sut.ReleasePackages.Commands;

/// <summary>
/// Recording that a package shipped.
/// </summary>
public sealed class MarkReleasePackageReleasedCommandHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly StatusRef _released = Status("Released", StatusCategory.Done, ProductStatusAlias.Released);

    public MarkReleasePackageReleasedCommandHandlerTests()
    {
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.ReleasePackage.Key, null, (int)ProductStatusAlias.Released, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(_released));
    }

    private MarkReleasePackageReleasedCommandHandler CreateSut() =>
        new(DbContext, _statusResolver.Object, CurrentUser.Object, Logger<MarkReleasePackageReleasedCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldRecordTheShipment()
    {
        // Arrange
        var product = SeedProduct();
        var package = SeedReleasePackage(product.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new MarkReleasePackageReleasedCommand(package.Id, new LocalDate(2026, 6, 2)),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        package.ReleasedDate.Should().Be(new LocalDate(2026, 6, 2));
        package.StatusCategory.Should().Be(StatusCategory.Done);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenThePackageDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new MarkReleasePackageReleasedCommand(Guid.CreateVersion7(), new LocalDate(2026, 6, 2)),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Release package not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenThePackageIsWithdrawn()
    {
        // Arrange
        var product = SeedProduct();
        var package = SeedReleasePackage(
            product.Id,
            status: Status("Withdrawn", StatusCategory.Removed, ProductStatusAlias.Withdrawn));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new MarkReleasePackageReleasedCommand(package.Id, new LocalDate(2026, 6, 2)),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A withdrawn package cannot be released.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
