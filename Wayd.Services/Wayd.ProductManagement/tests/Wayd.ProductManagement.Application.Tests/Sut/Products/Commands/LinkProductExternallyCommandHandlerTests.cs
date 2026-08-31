using FluentAssertions;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

/// <summary>
/// Pointing a product at the record that owns it in another system.
/// </summary>
public sealed class LinkProductExternallyCommandHandlerTests : ProductCommandTestBase
{
    private LinkProductExternallyCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, Logger<LinkProductExternallyCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldSetTheLink()
    {
        // Arrange
        var product = SeedProduct("Checkout", externalId: null);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new LinkProductExternallyCommand(product.Id, "acme/checkout"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.ExternalId.Should().Be("acme/checkout");
        product.DomainEvents.OfType<ProductLinkedExternallyEvent>().Should().ContainSingle();
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldClearTheLink_WhenOmitted()
    {
        // Arrange
        var product = SeedProduct("Checkout", externalId: "acme/checkout");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new LinkProductExternallyCommand(product.Id, null),
            TestContext.Current.CancellationToken);

        // Assert
        // Unlinking is the point of sending no value, not an omission to ignore.
        result.IsSuccess.Should().BeTrue();
        product.ExternalId.Should().BeNull();
        product.DomainEvents.OfType<ProductLinkedExternallyEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldLeaveTheNameAndDescriptionAlone()
    {
        // Splitting the facet is worth nothing if the handler still rewrites the rest.
        // Arrange
        var product = SeedProduct("Checkout", description: "The checkout product.");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new LinkProductExternallyCommand(product.Id, "acme/checkout"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Name.Should().Be("Checkout");
        product.Description.Should().Be("The checkout product.");
    }

    [Fact]
    public async Task Handle_ShouldRaiseNoEvent_WhenTheLinkIsUnchanged()
    {
        // Arrange
        var product = SeedProduct("Checkout", externalId: "acme/checkout");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new LinkProductExternallyCommand(product.Id, "acme/checkout"),
            TestContext.Current.CancellationToken);

        // Assert
        // An event asserts something happened; relinking to the same record did not.
        result.IsSuccess.Should().BeTrue();
        product.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProductDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new LinkProductExternallyCommand(Guid.CreateVersion7(), "acme/checkout"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
