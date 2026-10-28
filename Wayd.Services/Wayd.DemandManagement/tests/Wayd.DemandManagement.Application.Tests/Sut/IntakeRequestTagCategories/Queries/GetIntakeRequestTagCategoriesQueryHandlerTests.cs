using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories.Dtos;
using Wayd.DemandManagement.Application.IntakeRequestTagCategories.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequestTagCategories.Queries;

public sealed class GetIntakeRequestTagCategoriesQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private GetIntakeRequestTagCategoriesQueryHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object);

    [Fact]
    public async Task Handle_ByDefault_ReturnsActiveAxesInOrderWithEveryTagAlphabetically()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee());
        var segment = new IntakeRequestTagCategoryFaker().WithName("Segment").WithOrder(2).WithTags("SMB", "Enterprise").Generate();
        var area = new IntakeRequestTagCategoryFaker().WithName("Area").WithOrder(1).WithTags("Payments").Generate();
        var retired = new IntakeRequestTagCategoryFaker().WithName("Retired").WithOrder(3).WithIsActive(false).Generate();
        _scenario.DbContext.AddTagCategories(segment, area, retired);
        segment.DeactivateTag(segment.Tags.Single(t => t.Name == "SMB").Id, EventActor.System, _scenario.Clock.Now);

        // Act
        var result = await Handle(new GetIntakeRequestTagCategoriesQuery());

        // Assert
        result.Select(c => c.Name).Should().Equal("Area", "Segment");
        result[1].Tags.Select(t => (t.Name, t.IsActive)).Should().Equal(("Enterprise", true), ("SMB", false));
    }

    [Fact]
    public async Task Handle_IncludingInactive_ReturnsInactiveAxesToo()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee());
        _scenario.DbContext.AddTagCategories(new IntakeRequestTagCategoryFaker().WithIsActive(false).Generate());

        // Act
        var result = await Handle(new GetIntakeRequestTagCategoriesQuery(IncludeInactive: true));

        // Assert
        result.Should().ContainSingle().Which.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_CountsOnlyTheRequestsTheReaderCanSee()
    {
        // Arrange — a private desk's requests appear in no count for outsiders
        var area = _scenario.TagCategory(allowsMany: true, "Payments");
        _scenario.Tagged(_scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee()), area, "Payments");
        _scenario.Tagged(_scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee()), area, "Payments");
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await Handle(new GetIntakeRequestTagCategoriesQuery());

        // Assert
        result.Single().Tags.Single().RequestCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ForADesk_ReturnsOnlyTheActiveAxesItOffers()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee());
        var desk = _scenario.Desk();
        var chosen = _scenario.TagCategory();
        var inactiveChosen = _scenario.TagCategory();
        inactiveChosen.Deactivate(EventActor.System, _scenario.Clock.Now);
        _scenario.TagCategory();
        desk.ChangeTagCategories(false, [chosen.Id, inactiveChosen.Id], EventActor.System, _scenario.Clock.Now);

        // Act
        var result = await Handle(new GetIntakeRequestTagCategoriesQuery(IncludeInactive: true, DeskId: desk.Id));

        // Assert
        result.Select(c => c.Id).Should().Equal(chosen.Id);
    }

    private Task<List<IntakeRequestTagCategoryDto>> Handle(GetIntakeRequestTagCategoriesQuery query) =>
        CreateHandler().Handle(query, TestContext.Current.CancellationToken);
}
