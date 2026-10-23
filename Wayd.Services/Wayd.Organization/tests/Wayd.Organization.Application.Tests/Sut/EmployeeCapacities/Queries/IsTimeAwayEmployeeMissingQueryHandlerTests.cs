using Wayd.Common.Domain.Tests.Data;
using Wayd.Organization.Application.EmployeeCapacities.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;

namespace Wayd.Organization.Application.Tests.Sut.EmployeeCapacities.Queries;

public class IsTimeAwayEmployeeMissingQueryHandlerTests
{
    private readonly FakeOrganizationDbContext _dbContext = new();

    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public async Task Handle_UnknownEmployee_IsMissingOnlyForATimeAwayAdministrator(bool timeAwayAdministrator, bool expected)
    {
        // Arrange
        var caller = new EmployeeFaker().Generate();
        _dbContext.AddEmployee(caller);
        var handler = new IsTimeAwayEmployeeMissingQueryHandler(_dbContext, TestPrincipal.LinkedTo(caller.Id, timeAwayAdministrator));

        // Act
        var result = await handler.Handle(new IsTimeAwayEmployeeMissingQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public async Task Handle_KnownEmployee_IsNotMissingForATimeAwayAdministrator()
    {
        // Arrange
        var employee = new EmployeeFaker().Generate();
        _dbContext.AddEmployee(employee);
        var handler = new IsTimeAwayEmployeeMissingQueryHandler(_dbContext, TestPrincipal.LinkedTo(null, timeAwayAdministrator: true));

        // Act
        var result = await handler.Handle(new IsTimeAwayEmployeeMissingQuery(employee.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeFalse();
    }
}
