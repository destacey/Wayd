using FluentAssertions;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

/// <summary>
/// Deleting a product node. The aggregate raises the event and refuses; the handler owns the two
/// queries it refuses on, and performs the delete itself.
/// </summary>
public sealed class RemoveProductCommandHandlerTests : ProductCommandTestBase
{
    private RemoveProductCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, Logger<RemoveProductCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldRemoveTheProduct()
    {
        // Arrange
        var product = SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new RemoveProductCommand(product.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.Products.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRaiseTheRemovedEvent()
    {
        // Arrange
        var product = SeedProduct();
        var sut = CreateSut();

        // Act
        await sut.Handle(new RemoveProductCommand(product.Id), TestContext.Current.CancellationToken);

        // Assert
        product.DomainEvents.OfType<ProductRemovedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldRefuseAProductWithChildren()
    {
        // Arrange
        var parent = SeedProduct("Suite");
        SeedProduct("Checkout", parent.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new RemoveProductCommand(parent.Id), TestContext.Current.CancellationToken);

        // Assert
        // Deleting it would orphan the subtree, since a child holds its parent by id.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This product has child products and cannot be removed. Move or remove them first.");
        DbContext.Products.Should().HaveCount(2);
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldRefuseAProductWithReleases()
    {
        // Arrange
        var product = SeedProduct();
        SeedVersion(product.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new RemoveProductCommand(product.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This product has versions and cannot be removed.");
        DbContext.Products.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldRaiseNoEvent_WhenRefused()
    {
        // Arrange
        var parent = SeedProduct("Suite");
        SeedProduct("Checkout", parent.Id);
        var sut = CreateSut();

        // Act
        await sut.Handle(new RemoveProductCommand(parent.Id), TestContext.Current.CancellationToken);

        // Assert
        // The aggregate is still tracked, so a lingering event would be published by the next save of
        // any other change.
        parent.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldIgnoreChildrenOfOtherProducts()
    {
        // Arrange
        var product = SeedProduct("Checkout");
        var otherParent = SeedProduct("Suite");
        SeedProduct("Search", otherParent.Id);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new RemoveProductCommand(product.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProductDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new RemoveProductCommand(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product not found.");
    }
}
