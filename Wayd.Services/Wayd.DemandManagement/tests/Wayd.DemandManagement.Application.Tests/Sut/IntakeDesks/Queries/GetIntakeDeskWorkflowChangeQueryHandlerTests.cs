using CSharpFunctionalExtensions;
using FluentAssertions;
using Moq;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.StatusWorkflows;
using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.DemandManagement.Application.IntakeDesks;
using Wayd.DemandManagement.Application.IntakeDesks.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeDesks.Queries;

public sealed class GetIntakeDeskWorkflowChangeQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();
    private readonly Mock<IScopedWorkflowAssignments> _assignments = new();
    private readonly Guid _workflowId = Guid.NewGuid();

    private readonly ScopedWorkflowChangePreviewDto _preview = new()
    {
        Remap = new StatusRemapPreviewDto
        {
            From = new NavigationDto { Id = Guid.NewGuid(), Key = 1, Name = "Current" },
            To = new NavigationDto { Id = Guid.NewGuid(), Key = 2, Name = "Target" },
            Entries = [],
            IsComplete = true,
            AffectedRecordCount = 4,
        },
        TargetStatuses = [],
    };

    public GetIntakeDeskWorkflowChangeQueryHandlerTests()
    {
        _assignments.Setup(a => a.PreviewChange(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(_preview));
    }

    private GetIntakeDeskWorkflowChangeQueryHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _assignments.Object);

    [Fact]
    public async Task Handle_ForADeskAdmin_PreviewsTheDesksChange()
    {
        // Arrange
        var deskAdmin = _scenario.Employee();
        var desk = _scenario.DeskAdministeredBy(deskAdmin.Id);
        _scenario.SignedInAs(deskAdmin);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeDeskWorkflowChangeQuery(desk.Id, _workflowId), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().BeSameAs(_preview);
        _assignments.Verify(a => a.PreviewChange(DemandManagementWorkflowOwners.IntakeRequest.Key, desk.Id, _workflowId,
            It.IsAny<CancellationToken>()), Times.Once());
    }

    [Fact]
    public async Task Handle_ForAnAgent_RefusesWithoutCountingTheDesksRequests()
    {
        // Arrange — the counts say what is on a private desk
        var agent = _scenario.Employee();
        var desk = _scenario.Desk(agentIds: agent.Id);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeDeskWorkflowChangeQuery(desk.Id, _workflowId), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.NotADeskAdminError);
        _assignments.Verify(a => a.PreviewChange(It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never());
    }

    [Fact]
    public async Task Handle_ForADeskThatDoesNotExist_ReportsNotFound()
    {
        // Arrange
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.Administer);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeDeskWorkflowChangeQuery(Guid.NewGuid(), _workflowId), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeDeskAccess.NotFoundError);
    }
}
