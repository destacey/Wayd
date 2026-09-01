using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Application.ProductTagCategories.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.ProductTagCategories.Commands;

/// <summary>
/// Editing a tag axis. AllowsMany is deliberately not editable.
/// </summary>
public sealed class UpdateProductTagCategoryCommandHandlerTests : ProductCommandTestBase
{
    private UpdateProductTagCategoryCommandHandler UpdateSut() =>
        new(DbContext, Logger<UpdateProductTagCategoryCommandHandler>());

    private ProductTagCategory SeedSystemCategory(string name = "Platform")
    {
        var category = ProductTagCategory.CreateSystem(name, null, true, 1);
        DbContext.AddProductTagCategory(category);

        return category;
    }

    [Fact]
    public async Task Update_ShouldChangeTheDetails()
    {
        // Arrange
        var (category, _) = SeedTag(categoryName: "Platform");
        var sut = UpdateSut();

        // Act
        var result = await sut.Handle(
            new UpdateProductTagCategoryCommand(category.Id, "Target Platform", "Where it runs."),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        category.Name.Should().Be("Target Platform");
    }

    [Fact]
    public async Task Update_ShouldRefuseASystemAxis()
    {
        // Arrange
        var category = SeedSystemCategory();
        var sut = UpdateSut();

        // Act
        var result = await sut.Handle(
            new UpdateProductTagCategoryCommand(category.Id, "Renamed", null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("System tag categories cannot be modified.");
        category.Name.Should().Be("Platform");
    }

    [Fact]
    public async Task Update_ShouldAllowAnAxisToKeepItsOwnName()
    {
        // Arrange
        var (category, _) = SeedTag(categoryName: "Platform");
        var sut = UpdateSut();

        // Act
        var result = await sut.Handle(
            new UpdateProductTagCategoryCommand(category.Id, "Platform", "Now described."),
            TestContext.Current.CancellationToken);

        // Assert
        // The uniqueness check must exclude the row being edited, or changing only the description
        // would be refused.
        result.IsSuccess.Should().BeTrue();
        category.Description.Should().Be("Now described.");
    }
}
