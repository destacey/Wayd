using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Application.ProductTagCategories.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.ProductTagCategories.Commands;

/// <summary>
/// Removing an axis outright. Only safe for one nothing is tagged along.
/// </summary>
public sealed class DeleteProductTagCategoryCommandHandlerTests : ProductCommandTestBase
{
    private DeleteProductTagCategoryCommandHandler DeleteSut() =>
        new(DbContext, Logger<DeleteProductTagCategoryCommandHandler>());

    private ProductTagCategory SeedSystemCategory(string name = "Platform")
    {
        var category = ProductTagCategory.CreateSystem(name, null, true, 1);
        DbContext.AddProductTagCategory(category);

        return category;
    }

    [Fact]
    public async Task Delete_ShouldRemoveAnUnusedAxis()
    {
        // Arrange
        var (category, _) = SeedTag();
        var sut = DeleteSut();

        // Act
        var result = await sut.Handle(
            new DeleteProductTagCategoryCommand(category.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        DbContext.ProductTagCategories.Should().BeEmpty();
    }

    [Fact]
    public async Task Delete_ShouldRefuseAnAxisProductsAreTaggedAlong()
    {
        // Arrange
        var (category, tag) = SeedTag();
        var product = SeedProduct();
        product.Tag(tag, category, EventActor.System, Now);
        foreach (var assignment in product.Tags)
        {
            DbContext.AddProductTagAssignment(assignment);
        }

        var sut = DeleteSut();

        // Act
        var result = await sut.Handle(
            new DeleteProductTagCategoryCommand(category.Id), TestContext.Current.CancellationToken);

        // Assert
        // Deleting cascades to the tags and would strip the labels from every product carrying them.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Products are tagged along this axis and it cannot be deleted. Deactivate it instead.");
        DbContext.ProductTagCategories.Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_ShouldRefuseASystemAxis()
    {
        // Arrange
        var category = SeedSystemCategory();
        var sut = DeleteSut();

        // Act
        var result = await sut.Handle(
            new DeleteProductTagCategoryCommand(category.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("System tag categories cannot be deleted. Deactivate it instead.");
    }
}
