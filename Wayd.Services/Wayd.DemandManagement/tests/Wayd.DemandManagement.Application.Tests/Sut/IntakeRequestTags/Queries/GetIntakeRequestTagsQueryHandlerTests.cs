using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequestTags.Dtos;
using Wayd.DemandManagement.Application.IntakeRequestTags.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequestTags.Queries;

public sealed class GetIntakeRequestTagsQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private GetIntakeRequestTagsQueryHandler CreateHandler() => new(_scenario.DbContext, _scenario.Principal.Object);

    [Fact]
    public async Task Handle_ReturnsBothKindsSortedByName_WithTheirAxisAndVisibleUse()
    {
        // Arrange
        var area = _scenario.TagCategory(allowsMany: true, "Payments");
        var request = _scenario.Request(_scenario.AllowingFreeTags(_scenario.Desk()), _scenario.Employee());
        var free = _scenario.FreeTag("follow up");
        _scenario.TaggedFree(request, free);
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await Handle(new GetIntakeRequestTagsQuery());

        // Assert
        result.Select(t => t.Name).Should().Equal("follow up", "Payments");
        result[0].IsFree.Should().BeTrue();
        result[0].CategoryId.Should().BeNull();
        result[0].RequestCount.Should().Be(1);
        result[1].CategoryName.Should().Be(area.Name);
    }

    [Fact]
    public async Task Handle_AFreeTagOnlyOnAPrivateDesksRequests_IsHiddenFromOutsiders()
    {
        // Arrange — an agent typed it on that desk, and its name says something about the desk's requests
        var request = _scenario.Request(_scenario.AllowingFreeTags(_scenario.Desk(IntakeDeskVisibility.Private)), _scenario.Employee());
        _scenario.TaggedFree(request, _scenario.FreeTag("acme reorg"));
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await Handle(new GetIntakeRequestTagsQuery(IsFree: true));

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_TheDemandManagementAdmin_SeesEveryFreeTag()
    {
        // Arrange
        var request = _scenario.Request(_scenario.AllowingFreeTags(_scenario.Desk(IntakeDeskVisibility.Private)), _scenario.Employee());
        _scenario.TaggedFree(request, _scenario.FreeTag("acme reorg"));
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await Handle(new GetIntakeRequestTagsQuery(IsFree: true));

        // Assert
        result.Should().ContainSingle().Which.Name.Should().Be("acme reorg");
    }

    [Fact]
    public async Task Handle_WithASearch_KeepsNamesContainingItIgnoringCase()
    {
        // Arrange
        _scenario.TagCategory(allowsMany: true, "Payments", "Onboarding");
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await Handle(new GetIntakeRequestTagsQuery(Search: "PAY"));

        // Assert
        result.Should().ContainSingle().Which.Name.Should().Be("Payments");
    }

    private Task<List<IntakeRequestTagListItemDto>> Handle(GetIntakeRequestTagsQuery query) =>
        CreateHandler().Handle(query, TestContext.Current.CancellationToken);
}
