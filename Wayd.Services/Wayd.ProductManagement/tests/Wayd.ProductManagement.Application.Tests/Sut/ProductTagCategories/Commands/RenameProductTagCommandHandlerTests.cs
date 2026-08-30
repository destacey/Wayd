using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Application.ProductTagCategories.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.ProductTagCategories.Commands;

/// <summary>
/// Renaming a tag. Safe on one already in use, because products reference it by id.
/// </summary>
public sealed class RenameProductTagCommandHandlerTests : ProductCommandTestBase
{
    private RenameProductTagCommandHandler RenameSut() =>
        new(DbContext, Logger<RenameProductTagCommandHandler>());

    [Fact]
    public async Task Rename_ShouldChangeTheName()
    {
        // Arrange
        var (category, tag) = SeedTag(tagName: "ios");
        var sut = RenameSut();

        // Act
        var result = await sut.Handle(
            new RenameProductTagCommand(category.Id, tag.Id, "iOS", null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        tag.Name.Should().Be("iOS");
    }

    [Fact]
    public async Task Rename_ShouldFail_ForATagOnAnotherAxis()
    {
        // Arrange
        var (category, _) = SeedTag(categoryName: "Platform", tagName: "ios");
        var (_, foreign) = SeedTag(categoryName: "Tech Stack", tagName: "dotnet");
        var sut = RenameSut();

        // Act
        var result = await sut.Handle(
            new RenameProductTagCommand(category.Id, foreign.Id, "net", null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("That tag does not belong to this axis.");
    }
}
