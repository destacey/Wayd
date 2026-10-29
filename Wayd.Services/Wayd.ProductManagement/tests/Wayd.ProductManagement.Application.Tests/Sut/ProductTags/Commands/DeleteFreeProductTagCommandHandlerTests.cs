using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Common.Domain.Tagging;
using Wayd.ProductManagement.Application.ProductTags.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;

namespace Wayd.ProductManagement.Application.Tests.Sut.ProductTags.Commands;

/// <summary>
/// Deleting a free tag outright, taking it off whatever carries it.
/// </summary>
public sealed class DeleteFreeProductTagCommandHandlerTests : ProductCommandTestBase
{
    private DeleteFreeProductTagCommandHandler CreateSut() =>
        new(DbContext, CurrentUser.Object, Logger<DeleteFreeProductTagCommandHandler>(), DateTimeProvider.Object);

    [Fact]
    public async Task Handle_ShouldUntagEveryCarrierAndDeleteTheTag()
    {
        // Arrange
        var product = SeedProduct();
        var free = SeedFreeTag();
        product.Tag(free, null, EventActor.System, Now);
        product.ClearDomainEvents();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DeleteFreeProductTagCommand(free.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        product.Tags.Should().BeEmpty();
        product.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductTagsChangedEventV2>();
        DbContext.ProductTags.Should().NotContain(free);
        free.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<ProductFreeTagDeletedEvent>();
    }

    [Fact]
    public async Task Handle_ShouldRefuseATagOnAnAxis()
    {
        // Arrange
        var (_, ios) = SeedTag();
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(new DeleteFreeProductTagCommand(ios.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(TagErrors.NotAFreeTag);
        DbContext.ProductTags.Should().Contain(ios);
    }
}
