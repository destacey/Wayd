using FluentAssertions;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

/// <summary>
/// Labelling a product along an axis. The handler loads the tag's category alongside it, because the
/// axis decides whether a second tag joins the first or replaces it.
/// </summary>
public sealed class TagProductCommandHandlerTests : ProductCommandTestBase
{
    private TagProductCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, Logger<TagProductCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldApplyTheTag()
    {
        // Arrange
        var product = SeedProduct();
        var (_, tag) = SeedTag();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new TagProductCommand(product.Id, tag.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Tags.Should().ContainSingle().Which.TagId.Should().Be(tag.Id);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldKeepBoth_WhenTheAxisAllowsMany()
    {
        // Arrange
        var product = SeedProduct();
        var (category, ios) = SeedTag(allowsMany: true);
        var android = category.AddTag("android").Value;
        DbContext.AddProductTag(android);
        var sut = CreateSut();

        // Act
        await sut.Handle(new TagProductCommand(product.Id, ios.Id), TestContext.Current.CancellationToken);
        await sut.Handle(new TagProductCommand(product.Id, android.Id), TestContext.Current.CancellationToken);

        // Assert
        // A cross-platform app genuinely targets both; forcing a choice would record something false.
        product.Tags.Should().HaveCount(2);
    }

    [Fact]
    public async Task Handle_ShouldReplace_WhenTheAxisAllowsOne()
    {
        // Arrange
        var product = SeedProduct();
        var (category, ios) = SeedTag(allowsMany: false);
        var android = category.AddTag("android").Value;
        DbContext.AddProductTag(android);
        var sut = CreateSut();

        // Act
        await sut.Handle(new TagProductCommand(product.Id, ios.Id), TestContext.Current.CancellationToken);
        await sut.Handle(new TagProductCommand(product.Id, android.Id), TestContext.Current.CancellationToken);

        // Assert
        // Only correct because the handler loaded the category — the aggregate cannot see AllowsMany
        // without it, and would otherwise have kept both.
        product.Tags.Should().ContainSingle().Which.TagId.Should().Be(android.Id);
    }

    [Fact]
    public async Task Handle_ShouldRaiseTheTagsChangedEvent()
    {
        // Arrange
        var product = SeedProduct();
        var (_, tag) = SeedTag();
        var sut = CreateSut();

        // Act
        await sut.Handle(new TagProductCommand(product.Id, tag.Id), TestContext.Current.CancellationToken);

        // Assert
        var raised = product.DomainEvents.OfType<ProductTagsChangedEvent>().Should().ContainSingle().Subject;
        raised.TagIds.Should().BeEquivalentTo([tag.Id]);
    }

    [Fact]
    public async Task Handle_ShouldBeIdempotent()
    {
        // Arrange
        var product = SeedProduct();
        var (_, tag) = SeedTag();
        var sut = CreateSut();
        await sut.Handle(new TagProductCommand(product.Id, tag.Id), TestContext.Current.CancellationToken);

        // Act
        var result = await sut.Handle(new TagProductCommand(product.Id, tag.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Tags.Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheTagIsInactive()
    {
        // Arrange
        var product = SeedProduct();
        var (_, tag) = SeedTag(tagActive: false);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new TagProductCommand(product.Id, tag.Id), TestContext.Current.CancellationToken);

        // Assert
        // Deactivating retires a tag from new use without stripping it from what already carries it.
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("An inactive tag cannot be applied.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheAxisIsInactive()
    {
        // Arrange
        var product = SeedProduct();
        var (_, tag) = SeedTag(categoryActive: false);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new TagProductCommand(product.Id, tag.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("inactive");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheTagDoesNotExist()
    {
        // Arrange
        var product = SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new TagProductCommand(product.Id, Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Tag not found.");
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheProductDoesNotExist()
    {
        // Arrange
        var (_, tag) = SeedTag();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new TagProductCommand(Guid.CreateVersion7(), tag.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Product not found.");
    }
}
