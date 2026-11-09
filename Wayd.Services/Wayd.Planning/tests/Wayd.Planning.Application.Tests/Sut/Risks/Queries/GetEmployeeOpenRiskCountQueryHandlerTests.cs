using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.Risks.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.Risks.Queries;

public sealed class GetEmployeeOpenRiskCountQueryHandlerTests
{
    private readonly FakePlanningDbContext _dbContext = new();

    [Fact]
    public async Task Handle_CountsOnlyOpenRisksAssignedToTheEmployee()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        _dbContext.AddRisk(new RiskFaker().WithAssigneeId(employeeId).Generate());
        _dbContext.AddRisk(new RiskFaker().WithAssigneeId(employeeId).Generate());
        _dbContext.AddRisk(new RiskFaker().WithAssigneeId(employeeId).WithStatus(RiskStatus.Closed).Generate());
        _dbContext.AddRisk(new RiskFaker().WithAssigneeId(Guid.NewGuid()).Generate());
        _dbContext.AddRisk(new RiskFaker().WithAssigneeId(null).Generate());
        var sut = new GetEmployeeOpenRiskCountQueryHandler(_dbContext);

        // Act
        var result = await sut.Handle(new GetEmployeeOpenRiskCountQuery(employeeId), TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(2);
    }
}
