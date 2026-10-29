using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Application.ProductTags;
using Wayd.ProductManagement.Application.Products.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.Products.Commands;

/// <summary>
/// Tagging a product with a typed name: a tag on an axis when exactly one has it, otherwise a free tag.
/// </summary>
public sealed class TagProductByNameCommandHandlerTests : ProductCommandTestBase
{
    private TagProductByNameCommandHandler CreateSut() =>
        new(DbContext, TagSettings.Object, CurrentUser.Object, Logger<TagProductByNameCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldApplyTheTagOnAnAxis_WhenExactlyOneHasTheName()
    {
        // Arrange
        var product = SeedProduct();
        var (category, ios) = SeedTag(tagName: "ios");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new TagProductByNameCommand(product.Id, " IOS "), TestContext.Current.CancellationToken);

        // Assert
        // Typing a curated tag's name by hand applies it rather than starting a free tag beside it, free tags off or on.
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(ios.Id);
        product.Tags.Should().ContainSingle().Which.CategoryId.Should().Be(category.Id);
        DbSet().Should().ContainSingle();
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTwoAxesHaveTheName()
    {
        // Arrange
        AllowFreeTags();
        var product = SeedProduct();
        SeedTag(categoryName: "Platform", tagName: "ios");
        SeedTag(categoryName: "Tech Stack", tagName: "ios");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new TagProductByNameCommand(product.Id, "ios"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductTagErrors.AmbiguousName);
        product.Tags.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldRefuseANewName_WhenFreeTagsAreOff()
    {
        // Arrange
        var product = SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new TagProductByNameCommand(product.Id, "migration wave"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductTagErrors.FreeTagsTurnedOff);
        DbSet().Should().BeEmpty();
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldCreateAndApplyAFreeTag_WhenNoTagHasTheName()
    {
        // Arrange
        AllowFreeTags();
        var product = SeedProduct();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new TagProductByNameCommand(product.Id, "  migration   wave "), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var created = DbSet().Should().ContainSingle().Subject;
        created.Id.Should().Be(result.Value);
        created.IsFree.Should().BeTrue();
        created.Name.Should().Be("migration wave");
        product.Tags.Should().ContainSingle().Which.TagId.Should().Be(created.Id);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldReuseTheFreeTag_WhenOneHasTheNameInAnotherCase()
    {
        // Arrange
        AllowFreeTags();
        var product = SeedProduct();
        var existing = SeedFreeTag("Migration Wave");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new TagProductByNameCommand(product.Id, "migration wave"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(existing.Id);
        DbSet().Should().ContainSingle();
        existing.Name.Should().Be("Migration Wave");
    }

    private IEnumerable<ProductManagement.Domain.Models.ProductTag> DbSet() => DbContext.ProductTags.AsEnumerable();
}
