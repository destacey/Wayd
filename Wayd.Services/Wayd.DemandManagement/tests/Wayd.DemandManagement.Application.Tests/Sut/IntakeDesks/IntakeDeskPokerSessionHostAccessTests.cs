using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.DemandManagement.Application.IntakeDesks;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeDesks;

public sealed class IntakeDeskPokerSessionHostAccessTests
{
    private readonly IntakeScenario _scenario = new();

    private IntakeDeskPokerSessionHostAccess CreateSut() => new(_scenario.DbContext);

    [Fact]
    public void Kind_IsIntakeDesk()
    {
        // Act
        var kind = CreateSut().Kind;

        // Assert
        kind.Should().Be(PokerSessionHostKind.IntakeDesk);
    }

    [Fact]
    public async Task HostsManagedBy_ReturnsTheDesksTheEmployeeIsAnAgentOrAdminOnAndNoOthers()
    {
        // Arrange
        var employee = _scenario.Employee();
        var asAgent = _scenario.Desk(agentIds: employee.Id);
        var asAdmin = _scenario.DeskAdministeredBy(employee.Id);
        _scenario.DeskWithMembers(new IntakeDeskMemberDetails(_scenario.Employee().Id, IntakeDeskRole.Agent));

        // Act
        var hosts = await CreateSut().HostsManagedBy(employee.Id, TestContext.Current.CancellationToken);

        // Assert
        hosts.Should().BeEquivalentTo([asAgent.Id, asAdmin.Id]);
    }

    [Fact]
    public async Task HostsManagedBy_AnInactiveEmployee_ManagesNone()
    {
        // Arrange
        var employee = _scenario.Employee(isActive: false);
        _scenario.Desk(agentIds: employee.Id);

        // Act
        var hosts = await CreateSut().HostsManagedBy(employee.Id, TestContext.Current.CancellationToken);

        // Assert
        hosts.Should().BeEmpty();
    }
}
