using FluentAssertions;
using Wayd.DemandManagement.Application.Employees.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.Employees.Queries;

public sealed class GetIntakeEmployeeOptionsQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    [Fact]
    public async Task Handle_ReturnsActiveEmployeesOnly()
    {
        // Arrange
        var active = _scenario.Employee();
        _scenario.Employee(isActive: false);

        // Act
        var result = await new GetIntakeEmployeeOptionsQueryHandler(_scenario.DbContext)
            .Handle(new GetIntakeEmployeeOptionsQuery(), TestContext.Current.CancellationToken);

        // Assert
        var option = result.Should().ContainSingle().Subject;
        option.Id.Should().Be(active.Id);
        option.Name.Should().Be("Intake Probe");
    }
}
