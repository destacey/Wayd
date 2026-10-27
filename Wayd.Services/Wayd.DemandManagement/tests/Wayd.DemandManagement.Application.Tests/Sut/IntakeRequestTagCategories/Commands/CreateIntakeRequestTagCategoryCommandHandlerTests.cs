using FluentAssertions;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequestTagCategories.Commands;

public sealed class CreateIntakeRequestTagCategoryCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private CreateIntakeRequestTagCategoryCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_WhenValid_AddsTheAxisAfterTheLast()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);
        _scenario.DbContext.AddTagCategories(
            new IntakeRequestTagCategoryFaker().WithName("Area").WithOrder(1).Generate(),
            new IntakeRequestTagCategoryFaker().WithName("Cycle").WithOrder(3).WithIsActive(false).Generate());

        // Act
        var result = await CreateHandler().Handle(
            new CreateIntakeRequestTagCategoryCommand(" Customer segment ", null, AllowsMany: false),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var category = _scenario.DbContext.IntakeRequestTagCategories.Single(c => c.Id == result.Value);
        category.Name.Should().Be("Customer segment");
        category.AllowsMany.Should().BeFalse();
        category.Order.Should().Be(4);
        category.Tags.Should().BeEmpty();
        category.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestTagCategoryCreatedEvent>();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithTheNameOfAnInactiveAxis_FailsAndSavesNothing()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);
        _scenario.DbContext.AddTagCategories(new IntakeRequestTagCategoryFaker().WithName("Area").WithIsActive(false).Generate());

        // Act
        var result = await CreateHandler().Handle(new CreateIntakeRequestTagCategoryCommand("Area", null, true),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestTagCategoryErrors.DuplicateNameError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
