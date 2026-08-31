using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Application.ProductTagCategories.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.ProductTagCategories.Commands;

/// <summary>
/// Adding a tag to an axis. Routed through the category, which owns uniqueness within the axis.
/// </summary>
public sealed class AddProductTagCommandHandlerTests : ProductCommandTestBase
{
    private AddProductTagCommandHandler AddSut() =>
        new(DbContext, Logger<AddProductTagCommandHandler>());

    [Fact]
    public async Task Add_ShouldAddTheTag()
    {
        // Arrange
        var (category, _) = SeedTag(tagName: "ios");
        var sut = AddSut();

        // Act
        var result = await sut.Handle(
            new AddProductTagCommand(category.Id, "android", null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        category.Tags.Select(t => t.Name).Should().Contain("android");
    }

    [Fact]
    public async Task Add_ShouldFail_OnADuplicateNameOnTheSameAxis()
    {
        // Arrange
        var (category, _) = SeedTag(tagName: "ios");
        var sut = AddSut();

        // Act
        var result = await sut.Handle(
            new AddProductTagCommand(category.Id, "iOS", null), TestContext.Current.CancellationToken);

        // Assert
        // Only caught because the handler loaded the existing tags - the category cannot compare
        // against siblings it has not got.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A tag named 'iOS' already exists on this axis.");
    }

    [Fact]
    public async Task Add_ShouldFail_WhenTheAxisDoesNotExist()
    {
        // Arrange
        var sut = AddSut();

        // Act
        var result = await sut.Handle(
            new AddProductTagCommand(Guid.CreateVersion7(), "android", null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Tag category not found.");
    }
}
