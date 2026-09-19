using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Application.ProductTagCategories.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.ProductTagCategories.Commands;

/// <summary>
/// Removing a tag outright. Only safe for one no product carries.
/// </summary>
public sealed class DeleteProductTagCommandHandlerTests : ProductCommandTestBase
{
    private DeleteProductTagCommandHandler DeleteSut() =>
        new(DbContext, Logger<DeleteProductTagCommandHandler>());

    [Fact]
    public async Task Delete_ShouldRemoveAnUnusedTag()
    {
        // Arrange
        var (category, tag) = SeedTag();
        var sut = DeleteSut();

        // Act
        var result = await sut.Handle(
            new DeleteProductTagCommand(category.Id, tag.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        category.Tags.Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Delete_ShouldRefuseATagProductsCarry()
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
            new DeleteProductTagCommand(category.Id, tag.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Products carry this tag and it cannot be deleted. Deactivate it instead.");
        category.Tags.Should().ContainSingle();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Delete_ShouldFail_ForATagOnAnotherAxis()
    {
        // Arrange
        var (category, _) = SeedTag(categoryName: "Platform");
        var (foreignCategory, foreign) = SeedTag(categoryName: "Tech Stack", tagName: "dotnet");
        var sut = DeleteSut();

        // Act
        var result = await sut.Handle(
            new DeleteProductTagCommand(category.Id, foreign.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("That tag does not belong to this axis.");
        foreignCategory.Tags.Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_ShouldFail_WhenTheCategoryDoesNotExist()
    {
        // Arrange
        var sut = DeleteSut();

        // Act
        var result = await sut.Handle(
            new DeleteProductTagCommand(Guid.CreateVersion7(), Guid.CreateVersion7()),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Tag category not found.");
    }
}
