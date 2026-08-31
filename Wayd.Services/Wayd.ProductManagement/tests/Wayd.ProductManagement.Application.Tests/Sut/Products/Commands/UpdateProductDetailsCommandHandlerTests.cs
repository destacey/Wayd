using FluentAssertions;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

/// <summary>
/// Editing a product's descriptive fields. Whole-record semantics: an omitted value clears — the
/// external link excepted, which has its own command.
/// </summary>
public sealed class UpdateProductDetailsCommandHandlerTests : ProductCommandTestBase
{
    private UpdateProductDetailsCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, Logger<UpdateProductDetailsCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldUpdateTheDetails()
    {
        // Arrange
        var product = SeedProduct("Checkout", description: "Old", externalId: "repo/old");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new UpdateProductDetailsCommand(product.Id, "Payments", "New"),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Name.Should().Be("Payments");
        product.Description.Should().Be("New");
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldClearTheOptionalFields_WhenOmitted()
    {
        // Arrange
        var product = SeedProduct("Checkout", description: "Old", externalId: "repo/old");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new UpdateProductDetailsCommand(product.Id, "Checkout", null),
            TestContext.Current.CancellationToken);

        // Assert
        // Whole-record update, matching the API's PUT semantics — an omitted field is cleared, not kept.
        // The external link is not among them: it has its own endpoint, so a rename cannot drop it.
        result.IsSuccess.Should().BeTrue();
        product.Description.Should().BeNull();
        product.ExternalId.Should().Be("repo/old");
    }

    [Fact]
    public async Task Handle_ShouldRaiseNoEvent_WhenNothingChanged()
    {
        // Arrange
        var product = SeedProduct("Checkout", description: "Same", externalId: "repo/same");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new UpdateProductDetailsCommand(product.Id, "Checkout", "Same"),
            TestContext.Current.CancellationToken);

        // Assert
        // An event asserts something happened; re-saving an unedited form did not.
        result.IsSuccess.Should().BeTrue();
        product.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldRaiseTheUpdatedEvent()
    {
        // Arrange
        var product = SeedProduct("Checkout");
        var sut = CreateSut();

        // Act
        await sut.Handle(
            new UpdateProductDetailsCommand(product.Id, "Payments", null),
            TestContext.Current.CancellationToken);

        // Assert
        product.DomainEvents.OfType<ProductDetailsUpdatedEvent>().Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProductDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new UpdateProductDetailsCommand(Guid.CreateVersion7(), "Payments", null),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product not found.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
