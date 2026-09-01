using FluentAssertions;
using Wayd.ProductManagement.Application.ProductTagCategories.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.ProductTagCategories.Commands;

/// <summary>
/// Putting the tag axes in a given order.
/// </summary>
/// <remarks>
/// The command takes the whole set rather than one axis and a position, because ordering is relative:
/// moving one axis moves the others past it, and a caller that named only the moved one would leave
/// the rest overlapping it.
/// </remarks>
public sealed class ReorderProductTagCategoriesCommandHandlerTests : ProductCommandTestBase
{
    private ReorderProductTagCategoriesCommandHandler ReorderSut() =>
        new(DbContext, Logger<ReorderProductTagCategoriesCommandHandler>());

    private ProductTagCategory SeedCategory(string name, int order, bool isSystem = false)
    {
        var category = isSystem
            ? ProductTagCategory.CreateSystem(name, null, true, order)
            : ProductTagCategory.Create(name, null, true, order);
        DbContext.AddProductTagCategory(category);

        return category;
    }

    [Fact]
    public async Task Reorder_ShouldNumberTheAxesByTheirPositionInTheList()
    {
        // Arrange
        var platform = SeedCategory("Platform", 1);
        var techStack = SeedCategory("Tech Stack", 2);
        var compliance = SeedCategory("Compliance", 3);
        var sut = ReorderSut();

        // Act — compliance moves to the front
        var result = await sut.Handle(
            new ReorderProductTagCategoriesCommand([compliance.Id, platform.Id, techStack.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        compliance.Order.Should().Be(1);
        platform.Order.Should().Be(2);
        techStack.Order.Should().Be(3);
        DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Reorder_ShouldMoveASystemAxis()
    {
        // Arrange — a seeded axis is read-only in every other respect, but where it sits among the
        // others is the organization's call. Refusing this would pin it above their own axes for good.
        var seeded = SeedCategory("Platform", 1, isSystem: true);
        var own = SeedCategory("Compliance", 2);
        var sut = ReorderSut();

        // Act
        var result = await sut.Handle(
            new ReorderProductTagCategoriesCommand([own.Id, seeded.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        own.Order.Should().Be(1);
        seeded.Order.Should().Be(2);
    }

    [Fact]
    public async Task Reorder_ShouldRefuseAPartialList()
    {
        // Arrange — a caller working from a filtered or stale list would otherwise renumber part of
        // the set and leave the rest sharing positions with it.
        var platform = SeedCategory("Platform", 1);
        SeedCategory("Tech Stack", 2);
        var sut = ReorderSut();

        // Act
        var result = await sut.Handle(
            new ReorderProductTagCategoriesCommand([platform.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The order must list every tag category exactly once.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Reorder_ShouldRefuseAnUnknownAxis()
    {
        // Arrange — right count, wrong membership: the check has to be on the set, not its size
        var platform = SeedCategory("Platform", 1);
        SeedCategory("Tech Stack", 2);
        var sut = ReorderSut();

        // Act
        var result = await sut.Handle(
            new ReorderProductTagCategoriesCommand([platform.Id, Guid.NewGuid()]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("The order must list every tag category exactly once.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
