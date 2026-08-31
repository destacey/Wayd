using FluentAssertions;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

/// <summary>
/// Removing a label. Needs no lookup of the tag itself — removing one the product never carried is a
/// success that records nothing.
/// </summary>
public sealed class UntagProductCommandHandlerTests : ProductCommandTestBase
{
    private UntagProductCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, Logger<UntagProductCommandHandler>(), DateTimeProvider.Object);

    private TagProductCommandHandler TagSut() =>
        new(DbContext, CurrentUser.Object, Logger<TagProductCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldRemoveTheTag()
    {
        // Arrange
        var product = SeedProduct();
        var (_, tag) = SeedTag();
        await TagSut().Handle(new TagProductCommand(product.Id, tag.Id), TestContext.Current.CancellationToken);
        product.ClearDomainEvents();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new UntagProductCommand(product.Id, tag.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldRaiseTheTagsChangedEvent()
    {
        // Arrange
        var product = SeedProduct();
        var (_, tag) = SeedTag();
        await TagSut().Handle(new TagProductCommand(product.Id, tag.Id), TestContext.Current.CancellationToken);
        product.ClearDomainEvents();
        var sut = CreateSut();

        // Act
        await sut.Handle(new UntagProductCommand(product.Id, tag.Id), TestContext.Current.CancellationToken);

        // Assert
        var raised = product.DomainEvents.OfType<ProductTagsChangedEvent>().Should().ContainSingle().Subject;
        raised.TagIds.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldRaiseNothing_WhenTheTagWasNotCarried()
    {
        // Arrange
        var product = SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new UntagProductCommand(product.Id, Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        // An event asserts something happened; removing a tag that was not there did not.
        result.IsSuccess.Should().BeTrue();
        product.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldSucceed_ForATagThatNoLongerExists()
    {
        // Arrange
        var product = SeedProduct();
        var (_, tag) = SeedTag();
        await TagSut().Handle(new TagProductCommand(product.Id, tag.Id), TestContext.Current.CancellationToken);
        DbContext.ProductTags.Remove(tag);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new UntagProductCommand(product.Id, tag.Id), TestContext.Current.CancellationToken);

        // Assert
        // The handler deliberately does not look the tag up: a deleted tag must still be removable from
        // whatever still carries it.
        result.IsSuccess.Should().BeTrue();
        product.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProductDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new UntagProductCommand(Guid.CreateVersion7(), Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product not found.");
    }
}
