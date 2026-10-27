using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeDesks.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeDesks.Queries;

public sealed class GetIntakeDeskWorkflowQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();
    private readonly Mock<IScopedWorkflowAssignments> _assignments = new();

    public GetIntakeDeskWorkflowQueryHandlerTests()
    {
        _assignments.Setup(a => a.Current(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new ScopedWorkflow(
                new WorkflowChoice(_scenario.Workflow.Id, _scenario.Workflow.Key, _scenario.Workflow.Name, null), IsScopeOwn: false)));
    }

    private GetIntakeDeskWorkflowQueryHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _assignments.Object, _scenario.Statuses.Object);

    [Fact]
    public async Task Handle_ListsTheWorkflowsStatusesInItsOrder()
    {
        // Arrange — the board's columns, so a stage nobody is in still shows
        var desk = _scenario.Desk();
        _scenario.SignedInAs(_scenario.Employee());

        // Act
        var result = await CreateHandler().Handle(new GetIntakeDeskWorkflowQuery(new IdOrKey(desk.Id)), TestContext.Current.CancellationToken);

        // Assert
        result!.Statuses.Select(s => s.Name).Should().Equal(_scenario.Workflow.Statuses.Select(s => s.Name));
        result.Statuses.Should().Contain(s => s.Name == "In Review");
    }

    [Fact]
    public async Task Handle_ForADeskAdminOnAnEmptyDesk_OffersTheChange()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskAdministeredBy(deskAdmin.Id);
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeDeskWorkflowQuery(new IdOrKey(desk.Id)), TestContext.Current.CancellationToken);

        // Assert
        result!.Workflow.Id.Should().Be(_scenario.Workflow.Id);
        result.FollowsOrganizationDefault.Should().BeTrue();
        result.HasRequests.Should().BeFalse();
        result.CanChange.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_ForADeskAdminOnADeskWithRequests_SaysWhyItCannotChange()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskAdministeredBy(deskAdmin.Id);
        _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeDeskWorkflowQuery(new IdOrKey(desk.Id)), TestContext.Current.CancellationToken);

        // Assert
        result!.HasRequests.Should().BeTrue();
        result.CanChange.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ForAnOutsider_ShowsTheWorkflowButNotWhetherRequestsAreOnAPrivateDesk()
    {
        // Arrange
        var desk = _scenario.Desk(IntakeDeskVisibility.Private);
        _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeDeskWorkflowQuery(new IdOrKey(desk.Id)), TestContext.Current.CancellationToken);

        // Assert
        result!.Workflow.Name.Should().Be(_scenario.Workflow.Name);
        result.HasRequests.Should().BeFalse();
        result.CanChange.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ForADeskAdminWithAReadOnlyToken_DoesNotOfferTheChange()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskAdministeredBy(deskAdmin.Id);
        _scenario.SignedInAs(deskAdmin).WithReadOnlyToken();

        // Act
        var result = await CreateHandler().Handle(new GetIntakeDeskWorkflowQuery(new IdOrKey(desk.Id)), TestContext.Current.CancellationToken);

        // Assert
        result!.CanChange.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ForADeskThatDoesNotExist_ReturnsNull()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee());

        // Act
        var result = await CreateHandler().Handle(new GetIntakeDeskWorkflowQuery(new IdOrKey(Guid.NewGuid())), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }
}
