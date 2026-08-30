using FluentAssertions;
using Wayd.ProductManagement.Application.ProductTypes.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.ProductTypes.Commands;

/// <summary>
/// Removing a type outright. Only safe for one nothing carries.
/// </summary>
public sealed class DeleteProductTypeCommandHandlerTests : ProductCommandTestBase
{
    private DeleteProductTypeCommandHandler DeleteSut() =>
        new(DbContext, Logger<DeleteProductTypeCommandHandler>());

    private ProductType SeedSystemType(string name = "Application")
    {
        var productType = ProductType.CreateSystem(name, null, true, 1);
        DbContext.AddProductType(productType);

        return productType;
    }

    [Fact]
    public async Task Delete_ShouldRemoveAnUnusedType()
    {
        // Arrange
        var productType = SeedType();
        var sut = DeleteSut();

        // Act
        var result = await sut.Handle(
            new DeleteProductTypeCommand(productType.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.ProductTypes.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_ShouldRefuseATypeInUse()
    {
        // Arrange
        var productType = SeedType();
        SeedProduct(productTypeId: productType.Id);
        var sut = DeleteSut();

        // Act
        var result = await sut.Handle(
            new DeleteProductTypeCommand(productType.Id), TestContext.Current.CancellationToken);

        // Assert
        // The products holding it must keep resolving what they are.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This product type is in use and cannot be deleted. Deactivate it instead.");
        DbContext.ProductTypes.Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_ShouldRefuseASystemType()
    {
        // Arrange
        var productType = SeedSystemType();
        var sut = DeleteSut();

        // Act
        var result = await sut.Handle(
            new DeleteProductTypeCommand(productType.Id), TestContext.Current.CancellationToken);

        // Assert
        // An upgrade reseeds system types, so deleting one is undone on the next boot.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("System product types cannot be deleted. Deactivate it instead.");
    }

    [Fact]
    public async Task Delete_ShouldIgnoreProductsOfOtherTypes()
    {
        // Arrange
        var productType = SeedType("Service");
        var other = SeedType("Component");
        SeedProduct(productTypeId: other.Id);
        var sut = DeleteSut();

        // Act
        var result = await sut.Handle(
            new DeleteProductTypeCommand(productType.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
    }
}
