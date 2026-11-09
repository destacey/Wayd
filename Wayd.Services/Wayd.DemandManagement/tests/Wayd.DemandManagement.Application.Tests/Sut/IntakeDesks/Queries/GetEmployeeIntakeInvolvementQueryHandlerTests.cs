using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeDesks.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeDesks.Queries;

public sealed class GetEmployeeIntakeInvolvementQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private GetEmployeeIntakeInvolvementQueryHandler CreateHandler() => new(_scenario.DbContext);

    [Fact]
    public async Task Handle_CountsTheDesksTheEmployeeIsOn_ByRole()
    {
        // Arrange
        var employee = _scenario.Employee();
        var colleague = _scenario.Employee();
        _scenario.DeskAdministeredBy(employee.Id);
        _scenario.Desk(agentIds: [employee.Id, colleague.Id]);
        _scenario.Desk(agentIds: [employee.Id]);
        _scenario.DeskWithMembers(new IntakeDeskMemberDetails(colleague.Id, IntakeDeskRole.Admin));

        // Act
        var result = await CreateHandler().Handle(new GetEmployeeIntakeInvolvementQuery(employee.Id), TestContext.Current.CancellationToken);

        // Assert
        result.DeskCount.Should().Be(3);
        result.AdminDeskCount.Should().Be(1);
        result.AgentDeskCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_CountsOpenRequestsTheEmployeeRequestedOrSubmitted()
    {
        // Arrange
        var employee = _scenario.Employee();
        var colleague = _scenario.Employee();
        var desk = _scenario.Desk();
        _scenario.Request(desk, employee);
        _scenario.RequestIn(desk, employee, StatusCategory.Active);
        _scenario.RequestOnBehalf(desk, colleague, employee);
        _scenario.RequestIn(desk, employee, StatusCategory.Done);
        _scenario.Request(desk, colleague, employee.Id);

        // Act
        var result = await CreateHandler().Handle(new GetEmployeeIntakeInvolvementQuery(employee.Id), TestContext.Current.CancellationToken);

        // Assert
        result.OpenRequestsRaisedCount.Should().Be(3);
        result.DeskCount.Should().Be(0);
    }
}
