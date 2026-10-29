using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Application.ProductTags.Queries;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.ProductTags.Queries;

/// <summary>
/// Searching product tags across axes and free tags, for the tagging type-ahead and the free tag list.
/// </summary>
public sealed class GetProductTagsQueryHandlerTests : ProductCommandTestBase
{
    private GetProductTagsQueryHandler CreateSut() => new(DbContext);

    [Fact]
    public async Task Handle_ShouldReturnBothKinds_SortedByNameWithTheirAxisAndUse()
    {
        // Arrange
        var product = SeedProduct();
        var (platform, ios) = SeedTag(tagName: "ios");
        var free = SeedFreeTag("android tablets");
        product.Tag(free, null, EventActor.System, Now);
        SaveAssignments(product);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetProductTagsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(t => t.Name).Should().Equal("android tablets", "ios");
        var freeRow = result[0];
        freeRow.IsFree.Should().BeTrue();
        freeRow.CategoryId.Should().BeNull();
        freeRow.CategoryName.Should().BeNull();
        freeRow.ProductCount.Should().Be(1);
        result[1].CategoryName.Should().Be(platform.Name);
        result[1].Id.Should().Be(ios.Id);
    }

    [Fact]
    public async Task Handle_ShouldKeepOnlyFreeTagsMatchingTheSearch()
    {
        // Arrange
        SeedTag(tagName: "wave");
        SeedFreeTag("Migration Wave");
        SeedFreeTag("follow up");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new GetProductTagsQuery(Search: "WAVE", IsFree: true), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.Name.Should().Be("Migration Wave");
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutTagsThatCannotBeApplied_UnlessAskedFor()
    {
        // Arrange
        SeedTag(categoryName: "Retired", tagName: "legacy", categoryActive: false);
        var sut = CreateSut();

        // Act
        var forPicker = await sut.Handle(new GetProductTagsQuery(), TestContext.Current.CancellationToken);
        var all = await sut.Handle(new GetProductTagsQuery(IncludeInactive: true), TestContext.Current.CancellationToken);

        // Assert
        forPicker.Should().BeEmpty();
        all.Should().ContainSingle();
    }
}
