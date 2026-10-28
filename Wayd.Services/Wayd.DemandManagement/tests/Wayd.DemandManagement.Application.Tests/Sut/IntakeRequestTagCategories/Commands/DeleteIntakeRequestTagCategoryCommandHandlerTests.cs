using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequestTagCategories.Commands;

public sealed class DeleteIntakeRequestTagCategoryCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private DeleteIntakeRequestTagCategoryCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_WhenNoRequestIsTaggedAlongIt_DeletesItAndRaises()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);
        var area = _scenario.TagCategory(allowsMany: true, "Payments");

        // Act
        var result = await CreateHandler().Handle(new DeleteIntakeRequestTagCategoryCommand(area.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.DbContext.IntakeRequestTagCategories.Should().BeEmpty();
        area.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<IntakeRequestTagCategoryDeletedEvent>();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenARequestOnAPrivateDeskIsTaggedAlongIt_FailsAndKeepsIt()
    {
        // Arrange — in use counts every request, including ones the admin's view would not list
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);
        var area = _scenario.TagCategory(allowsMany: true, "Payments");
        var request = _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee());
        _scenario.Tagged(request, area, "Payments");

        // Act
        var result = await CreateHandler().Handle(new DeleteIntakeRequestTagCategoryCommand(area.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestTagCategory.AxisInUseError);
        _scenario.DbContext.IntakeRequestTagCategories.Should().ContainSingle();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAnUnknownAxis_ReportsNotFound()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new DeleteIntakeRequestTagCategoryCommand(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestTagCategoryErrors.NotFoundError);
    }
}
