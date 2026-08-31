using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Application.ProductTagCategories.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.ProductTagCategories.Commands;

/// <summary>
/// Retiring a whole axis, or restoring it.
/// </summary>
public sealed class SetProductTagCategoryActiveCommandHandlerTests : ProductCommandTestBase
{
    private SetProductTagCategoryActiveCommandHandler ActivationSut() =>
        new(DbContext, Logger<SetProductTagCategoryActiveCommandHandler>());

    [Fact]
    public async Task Deactivate_ShouldTakeTheAxisOutOfUse()
    {
        // Arrange
        var (category, _) = SeedTag();
        var sut = ActivationSut();

        // Act
        var result = await sut.Handle(
            new SetProductTagCategoryActiveCommand(category.Id, false), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        category.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Activate_ShouldFail_WhenAlreadyActive()
    {
        // Arrange
        var (category, _) = SeedTag();
        var sut = ActivationSut();

        // Act
        var result = await sut.Handle(
            new SetProductTagCategoryActiveCommand(category.Id, true), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This tag category is already active.");
    }
}
