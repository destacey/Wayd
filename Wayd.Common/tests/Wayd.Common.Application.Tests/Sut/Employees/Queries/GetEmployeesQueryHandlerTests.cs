using FluentAssertions;
using Wayd.Common.Application.Employees.Queries;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;

namespace Wayd.Common.Application.Tests.Sut.Employees.Queries;

public sealed class GetEmployeesQueryHandlerTests
{
    private readonly FakeWaydDbContext _dbContext = new();

    [Fact]
    public async Task Handle_OrdersByLastNameThenFirstNameThenId()
    {
        // Arrange — two employees share a full name, so only the id separates them
        var adams = new EmployeeFaker().WithName(new PersonName("Zoe", null, "Adams")).Generate();
        var bakerAnn = new EmployeeFaker().WithName(new PersonName("Ann", null, "Baker")).Generate();
        var bakerTomA = new EmployeeFaker().WithName(new PersonName("Tom", null, "Baker")).Generate();
        var bakerTomB = new EmployeeFaker().WithName(new PersonName("Tom", null, "Baker")).Generate();
        var toms = new[] { bakerTomA, bakerTomB }.OrderBy(e => e.Id).ToArray();
        foreach (var employee in new[] { toms[1], bakerAnn, adams, toms[0] })
            _dbContext.AddEmployee(employee);

        var handler = new GetEmployeesQueryHandler(_dbContext);

        // Act
        var result = await handler.Handle(new GetEmployeesQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(e => e.Id).Should().Equal(adams.Id, bakerAnn.Id, toms[0].Id, toms[1].Id);
    }
}
