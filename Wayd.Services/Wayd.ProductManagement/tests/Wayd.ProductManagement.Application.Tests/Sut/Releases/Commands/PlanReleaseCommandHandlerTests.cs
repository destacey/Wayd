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
/// Planning a release. The handler answers whether the product's type permits one, because the
/// aggregate cannot load the type.
/// </summary>
public sealed class PlanReleaseCommandHandlerTests : ProductCommandTestBase
{
    private readonly Mock<IStatusResolver> _statusResolver = new();

    public PlanReleaseCommandHandlerTests()
    {
        _statusResolver
            .Setup(r => r.Initial(ProductWorkflowOwners.Release.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(
                Status("Planned", StatusCategory.Proposed, ProductStatusAlias.None)));
    }

    private PlanReleaseCommandHandler CreateSut() =>
        new(DbContext, _statusResolver.Object, CurrentUser.Object, Logger<PlanReleaseCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldPlanTheRelease()
    {
        // Arrange
        var productType = SeedType(isReleasable: true);
        var product = SeedProduct(productTypeId: productType.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new PlanReleaseCommand(product.Id, "4.8.2", "Summer", null, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.Releases.Should().ContainSingle().Which.Version.Should().Be("4.8.2");
    }

    [Fact]
    public async Task Handle_ShouldRefuseANonReleasableProduct()
    {
        // Arrange
        var productType = SeedType(isReleasable: false);
        var product = SeedProduct(productTypeId: productType.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new PlanReleaseCommand(product.Id, "1.0", null, null, null), TestContext.Current.CancellationToken);

        // Assert
        // Whether releases are permitted is the type's answer, and only the handler can load it.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Releases cannot be cut against this product's type.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldKeepTheVersionExactlyAsWritten()
    {
        // Arrange
        var productType = SeedType(isReleasable: true);
        var product = SeedProduct(productTypeId: productType.Id);
        var sut = CreateSut();

        // Act
        await sut.Handle(
            new PlanReleaseCommand(product.Id, "v3-beta.1", null, null, null), TestContext.Current.CancellationToken);

        // Assert
        // Free text, never parsed: nothing normalises, sorts or extracts meaning from it.
        DbContext.Releases.Single().Version.Should().Be("v3-beta.1");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheWorkflowCannotBeResolved()
    {
        // Arrange
        var productType = SeedType(isReleasable: true);
        var product = SeedProduct(productTypeId: productType.Id);
        _statusResolver
            .Setup(r => r.Initial(ProductWorkflowOwners.Release.Key, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<StatusRef>("No workflow is assigned for Release."));

        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new PlanReleaseCommand(product.Id, "1.0", null, null, null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("No workflow is assigned for Release.");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProductDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new PlanReleaseCommand(Guid.CreateVersion7(), "1.0", null, null, null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product not found.");
    }
}
