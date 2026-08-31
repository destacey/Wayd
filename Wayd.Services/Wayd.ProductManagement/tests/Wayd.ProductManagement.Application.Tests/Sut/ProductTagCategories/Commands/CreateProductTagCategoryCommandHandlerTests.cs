using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.ProductManagement.Application.ProductTagCategories.Commands;
using Wayd.ProductManagement.Application.Tests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.ProductManagement.Application.Tests.Sut.ProductTagCategories.Commands;

/// <summary>
/// Creating a tag axis.
/// </summary>
public sealed class CreateProductTagCategoryCommandHandlerTests : ProductCommandTestBase
{
    private CreateProductTagCategoryCommandHandler CreateSut() =>
        new(DbContext, Logger<CreateProductTagCategoryCommandHandler>());

    [Fact]
    public async Task Create_ShouldAddTheAxis()
    {
        // Arrange
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CreateProductTagCategoryCommand("Compliance", "Regulatory scope.", false, 2),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var created = DbContext.ProductTagCategories.Should().ContainSingle().Subject;
        created.Name.Should().Be("Compliance");
        created.AllowsMany.Should().BeFalse();
    }

    [Fact]
    public async Task Create_ShouldFail_OnADuplicateName()
    {
        // Arrange
        SeedTag(categoryName: "Platform");
        var sut = CreateSut();

        // Act
        var result = await sut.Handle(
            new CreateProductTagCategoryCommand("Platform", null, true, 1), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("A tag category named 'Platform' already exists.");
        DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
