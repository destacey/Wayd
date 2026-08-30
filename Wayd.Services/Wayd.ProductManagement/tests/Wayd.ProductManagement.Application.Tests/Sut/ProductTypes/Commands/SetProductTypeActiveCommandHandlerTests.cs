using FluentAssertions;
using Wayd.ProductManagement.Application.ProductTypes.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.ProductTypes.Commands;

/// <summary>
/// Retiring a type from new use, or restoring it. Reversible, unlike deleting.
/// </summary>
public sealed class SetProductTypeActiveCommandHandlerTests : ProductCommandTestBase
{
    private SetProductTypeActiveCommandHandler ActivationSut() =>
        new(DbContext, Logger<SetProductTypeActiveCommandHandler>());

    private ProductType SeedSystemType(string name = "Application")
    {
        var productType = ProductType.CreateSystem(name, null, true, 1);
        DbContext.AddProductType(productType);

        return productType;
    }

    [Fact]
    public async Task Deactivate_ShouldTakeTheTypeOutOfUse()
    {
        // Arrange
        var productType = SeedType();
        var sut = ActivationSut();

        // Act
        var result = await sut.Handle(
            new SetProductTypeActiveCommand(productType.Id, false), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        productType.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Deactivate_ShouldBeAllowedForASystemType()
    {
        // Arrange
        var productType = SeedSystemType();
        var sut = ActivationSut();

        // Act
        var result = await sut.Handle(
            new SetProductTypeActiveCommand(productType.Id, false), TestContext.Current.CancellationToken);

        // Assert
        // An organization that does not ship libraries should be able to hide the seeded type without
        // the seeder recreating it — which is why this is not blocked like an edit is.
        result.IsSuccess.Should().BeTrue();
        productType.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Deactivate_ShouldFail_WhenAlreadyInactive()
    {
        // Arrange
        var productType = SeedType(isActive: false);
        var sut = ActivationSut();

        // Act
        var result = await sut.Handle(
            new SetProductTypeActiveCommand(productType.Id, false), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("This product type is already inactive.");
    }
}
