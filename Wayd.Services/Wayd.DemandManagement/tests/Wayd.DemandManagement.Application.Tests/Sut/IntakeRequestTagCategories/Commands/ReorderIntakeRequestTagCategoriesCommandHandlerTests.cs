using FluentAssertions;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequestTagCategories.Commands;

public sealed class ReorderIntakeRequestTagCategoriesCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private ReorderIntakeRequestTagCategoriesCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_WithEveryAxis_NumbersThemInTheGivenOrder()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);
        var area = new IntakeRequestTagCategoryFaker().WithOrder(1).Generate();
        var segment = new IntakeRequestTagCategoryFaker().WithOrder(2).WithIsActive(false).Generate();
        _scenario.DbContext.AddTagCategories(area, segment);

        // Act
        var result = await CreateHandler().Handle(new ReorderIntakeRequestTagCategoriesCommand([segment.Id, area.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        segment.Order.Should().Be(1);
        area.Order.Should().Be(2);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithAPartialList_FailsAndMovesNothing()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);
        var area = new IntakeRequestTagCategoryFaker().WithOrder(1).Generate();
        var segment = new IntakeRequestTagCategoryFaker().WithOrder(2).Generate();
        _scenario.DbContext.AddTagCategories(area, segment);

        // Act
        var result = await CreateHandler().Handle(new ReorderIntakeRequestTagCategoriesCommand([segment.Id]),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        segment.Order.Should().Be(2);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
