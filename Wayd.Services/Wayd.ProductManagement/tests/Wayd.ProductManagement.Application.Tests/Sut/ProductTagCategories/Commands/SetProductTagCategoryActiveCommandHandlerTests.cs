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
        new(DbContext, CurrentUser.Object, CurrentPrincipal.Object, DateTimeProvider.Object, Logger<SetProductTagCategoryActiveCommandHandler>());

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
    public async Task Activate_ShouldSucceedAndChangeNothing_WhenAlreadyActive()
    {
        // Arrange
        var (category, _) = SeedTag();
        var sut = ActivationSut();

        // Act
        var result = await sut.Handle(
            new SetProductTagCategoryActiveCommand(category.Id, true), TestContext.Current.CancellationToken);

        // Assert
        // Asking for the state it is already in is not an error, and an event would claim a change that did not happen.
        result.IsSuccess.Should().BeTrue();
        category.IsActive.Should().BeTrue();
        category.DomainEvents.Should().BeEmpty();
    }
}
