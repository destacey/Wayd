using FluentAssertions;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

/// <summary>
/// Changing what a product is. The handler owns the "does this already have releases?" query, because
/// the aggregate cannot run it.
/// </summary>
public sealed class RetypeProductCommandHandlerTests : ProductCommandTestBase
{
    private RetypeProductCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, Logger<RetypeProductCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldRetypeTheProduct()
    {
        // Arrange
        var from = SeedType("Component");
        var to = SeedType("Application");
        var product = SeedProduct(productTypeId: from.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new RetypeProductCommand(product.Id, to.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.ProductTypeId.Should().Be(to.Id);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRaiseTheRetypedEvent()
    {
        // Arrange
        var from = SeedType("Component");
        var to = SeedType("Application");
        var product = SeedProduct(productTypeId: from.Id);
        var sut = CreateSut();

        // Act
        await sut.Handle(new RetypeProductCommand(product.Id, to.Id), TestContext.Current.CancellationToken);

        // Assert
        product.DomainEvents.OfType<ProductRetypedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldRaiseNoEvent_WhenTheTypeIsUnchanged()
    {
        // Arrange
        var productType = SeedType("Component");
        var product = SeedProduct(productTypeId: productType.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new RetypeProductCommand(product.Id, productType.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldRefuseANonReleasableType_WhenReleasesExist()
    {
        // Arrange
        var from = SeedType("Application");
        var to = SeedType("Library", isReleasable: false);
        var product = SeedProduct(productTypeId: from.Id);
        SeedVersion(product.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new RetypeProductCommand(product.Id, to.Id), TestContext.Current.CancellationToken);

        // Assert
        // The releases already cut would be orphaned by a type that cannot carry them.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This product has versions and cannot be changed to a type that is not releasable.");
        product.ProductTypeId.Should().Be(from.Id);
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldAllowANonReleasableType_WhenNoReleasesExist()
    {
        // Arrange
        var from = SeedType("Application");
        var to = SeedType("Library", isReleasable: false);
        var product = SeedProduct(productTypeId: from.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new RetypeProductCommand(product.Id, to.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.ProductTypeId.Should().Be(to.Id);
    }

    [Fact]
    public async Task Handle_ShouldIgnoreReleasesOfOtherProducts()
    {
        // Arrange
        var from = SeedType("Application");
        var to = SeedType("Library", isReleasable: false);
        var product = SeedProduct(productTypeId: from.Id);
        var other = SeedProduct("Other", productTypeId: from.Id);
        SeedVersion(other.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new RetypeProductCommand(product.Id, to.Id), TestContext.Current.CancellationToken);

        // Assert
        // The query must be scoped to this product; a repo-wide "any releases?" would block every retype
        // as soon as one release existed anywhere.
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheTargetTypeIsInactive()
    {
        // Arrange
        var from = SeedType("Component");
        var to = SeedType("Application", isActive: false);
        var product = SeedProduct(productTypeId: from.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new RetypeProductCommand(product.Id, to.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("inactive");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheTypeDoesNotExist()
    {
        // Arrange
        var product = SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new RetypeProductCommand(product.Id, Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product Type not found.");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProductDoesNotExist()
    {
        // Arrange
        var productType = SeedType();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new RetypeProductCommand(Guid.CreateVersion7(), productType.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product not found.");
    }
}
