using FluentAssertions;
using Wayd.ProductManagement.Application.ProductTypes.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.ProductTypes.Commands;

/// <summary>
/// Editing a type. Two mutations run here, so the refusal they share is checked once up front.
/// </summary>
public sealed class UpdateProductTypeCommandHandlerTests : ProductCommandTestBase
{
    private UpdateProductTypeCommandHandler UpdateSut() =>
        new(DbContext, Logger<UpdateProductTypeCommandHandler>());

    private ProductType SeedSystemType(string name = "Application")
    {
        var productType = ProductType.CreateSystem(name, null, true, 1);
        DbContext.AddProductType(productType);

        return productType;
    }

    [Fact]
    public async Task Update_ShouldChangeTheDetailsAndReleasability()
    {
        // Arrange
        var productType = SeedType("Component", isReleasable: false);
        var sut = UpdateSut();

        // Act
        var result = await sut.Handle(
            new UpdateProductTypeCommand(productType.Id, "Library", "Shared code.", true, 3),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        productType.Name.Should().Be("Library");
        productType.IsReleasable.Should().BeTrue();
        productType.Order.Should().Be(3);
    }

    [Fact]
    public async Task Update_ShouldRefuseASystemType_WithoutApplyingAnything()
    {
        // Arrange
        var productType = SeedSystemType("Application");
        var sut = UpdateSut();

        // Act
        var result = await sut.Handle(
            new UpdateProductTypeCommand(productType.Id, "Renamed", null, false, 9),
            TestContext.Current.CancellationToken);

        // Assert
        // Two mutations share this refusal, so it is checked once up front — a name applied by the
        // first call and abandoned by the second would still be tracked and saved.
        result.IsFailure.Should().BeTrue();
        productType.Name.Should().Be("Application");
        productType.IsReleasable.Should().BeTrue();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Update_ShouldFail_OnAnotherTypesName()
    {
        // Arrange
        SeedType("Service");
        var productType = SeedType("Component");
        var sut = UpdateSut();

        // Act
        var result = await sut.Handle(
            new UpdateProductTypeCommand(productType.Id, "Service", null, true, 1),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A product type named 'Service' already exists.");
    }

    [Fact]
    public async Task Update_ShouldAllowATypeToKeepItsOwnName()
    {
        // Arrange
        var productType = SeedType("Service");
        var sut = UpdateSut();

        // Act
        var result = await sut.Handle(
            new UpdateProductTypeCommand(productType.Id, "Service", "Now described.", true, 1),
            TestContext.Current.CancellationToken);

        // Assert
        // The uniqueness check must exclude the row being edited, or changing only the description
        // would be refused.
        result.IsSuccess.Should().BeTrue();
        productType.Description.Should().Be("Now described.");
    }
}
