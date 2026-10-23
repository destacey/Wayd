using Wayd.Common.Domain.Tests.Data;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Queries;

public class GetEmployeeTeamMembershipsQueryHandlerTests
{
    private readonly FakeOrganizationDbContext _dbContext = new();

    [Fact]
    public async Task Handle_UnknownEmployee_ReturnsNull()
    {
        // Arrange
        _dbContext.AddEmployee(new EmployeeFaker().Generate());
        var handler = new GetEmployeeTeamMembershipsQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(new GetEmployeeTeamMembershipsQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_EmployeeOnNoTeam_ReturnsAnEmptyList()
    {
        // Arrange
        var employee = new EmployeeFaker().Generate();
        _dbContext.AddEmployee(employee);
        var handler = new GetEmployeeTeamMembershipsQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(new GetEmployeeTeamMembershipsQuery(employee.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull().And.BeEmpty();
    }
}
