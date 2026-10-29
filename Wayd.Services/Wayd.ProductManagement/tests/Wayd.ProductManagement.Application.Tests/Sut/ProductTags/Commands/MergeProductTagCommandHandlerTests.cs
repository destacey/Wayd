using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.Tagging;
using Wayd.ProductManagement.Application.ProductTags;
using Wayd.ProductManagement.Application.ProductTags.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.ProductTags.Commands;

/// <summary>
/// Folding a free tag into another tag, which is also how a free tag that caught on is promoted onto an axis.
/// </summary>
public sealed class MergeProductTagCommandHandlerTests : ProductCommandTestBase
{
    private MergeProductTagCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, Logger<MergeProductTagCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldMoveEveryProductToTheTargetAndDeleteTheFreeTag()
    {
        // Arrange
        var first = SeedProduct();
        var second = SeedProduct("Billing");
        var free = SeedFreeTag("iphone");
        var (platform, ios) = SeedTag(tagName: "ios");
        first.Tag(free, null, EventActor.System, Now);
        second.Tag(free, null, EventActor.System, Now);
        second.Tag(ios, platform, EventActor.System, Now);
        first.ClearDomainEvents();
        second.ClearDomainEvents();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new MergeProductTagCommand(free.Id, ios.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        first.Tags.Should().ContainSingle().Which.TagId.Should().Be(ios.Id);
        second.Tags.Should().ContainSingle().Which.TagId.Should().Be(ios.Id);
        first.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductTagsChangedEventV2>();
        DbContext.ProductTags.Should().NotContain(free);
        free.DomainEvents.OfType<ProductFreeTagDeletedEvent>().Should().ContainSingle()
            .Which.MergedIntoTagId.Should().Be(ios.Id);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheSourceIsOnAnAxis()
    {
        // Arrange
        var (_, ios) = SeedTag(tagName: "ios");
        var free = SeedFreeTag("iphone");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new MergeProductTagCommand(ios.Id, free.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TagErrors.NotAFreeTag);
    }

    [Fact]
    public async Task Handle_ShouldRefuse_WhenTheTargetIsInactive()
    {
        // Arrange
        var free = SeedFreeTag("iphone");
        var (_, ios) = SeedTag(tagName: "ios", tagActive: false);
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new MergeProductTagCommand(free.Id, ios.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(ProductTagErrors.InactiveMergeTarget);
        DbContext.ProductTags.Should().Contain(free);
    }
}
