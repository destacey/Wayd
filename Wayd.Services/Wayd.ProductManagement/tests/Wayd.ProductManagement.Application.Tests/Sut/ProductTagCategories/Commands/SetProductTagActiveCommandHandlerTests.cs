using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Application.ProductTagCategories.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.ProductTagCategories.Commands;

/// <summary>
/// Retiring a tag from new use. Products already carrying it keep it.
/// </summary>
public sealed class SetProductTagActiveCommandHandlerTests : ProductCommandTestBase
{
    private SetProductTagActiveCommandHandler ActivationSut() =>
        new(DbContext, Logger<SetProductTagActiveCommandHandler>());

    [Fact]
    public async Task Deactivate_ShouldTakeTheTagOutOfUse()
    {
        // Arrange
        var (category, tag) = SeedTag();
        var sut = ActivationSut();

        // Act
        var result = await sut.Handle(
            new SetProductTagActiveCommand(category.Id, tag.Id, false), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        tag.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Deactivate_ShouldFail_ForATagOnAnotherAxis()
    {
        // Arrange
        var (category, _) = SeedTag(categoryName: "Platform");
        var (_, foreign) = SeedTag(categoryName: "Tech Stack", tagName: "dotnet");
        var sut = ActivationSut();

        // Act
        var result = await sut.Handle(
            new SetProductTagActiveCommand(category.Id, foreign.Id, false), TestContext.Current.CancellationToken);

        // Assert
        // Scoped to the category so a tag id from another axis cannot be reached through this route.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("That tag does not belong to this axis.");
        foreign.IsActive.Should().BeTrue();
    }
}
