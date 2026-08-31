using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.StatusWorkflows;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Application.ReleasePackages.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Tests.Sut.ReleasePackages.Commands;

/// <summary>
/// Pulling a package after it was assembled.
/// </summary>
public sealed class WithdrawReleasePackageCommandHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();
    private readonly StatusRef _withdrawn = Status("Withdrawn", StatusCategory.Removed, ProductStatusAlias.Withdrawn);

    public WithdrawReleasePackageCommandHandlerTests()
    {
        _statusResolver
            .Setup(r => r.ForAlias(
                ProductWorkflowOwners.ReleasePackage.Key, null, (int)ProductStatusAlias.Withdrawn, It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => Result.Success(_withdrawn));
    }

    private WithdrawReleasePackageCommandHandler CreateSut() =>
        new(DbContext, _statusResolver.Object, CurrentUser.Object, Logger<WithdrawReleasePackageCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldWithdrawThePackage()
    {
        // Arrange
        var product = SeedProduct();
        var package = SeedReleasePackage(product.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new WithdrawReleasePackageCommand(package.Id, "Failed smoke tests."),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        package.StatusCategory.Should().Be(StatusCategory.Removed);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenAlreadyWithdrawn()
    {
        // Arrange
        var product = SeedProduct();
        var package = SeedReleasePackage(
            product.Id,
            status: Status("Withdrawn", StatusCategory.Removed, ProductStatusAlias.Withdrawn));
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new WithdrawReleasePackageCommand(package.Id, "Again."),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This package has already been withdrawn.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenThePackageDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new WithdrawReleasePackageCommand(Guid.CreateVersion7(), null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Release package not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
