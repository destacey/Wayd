using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Wayd.Common.Application.Employees.Queries;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Tests.Data;
using Wayd.Common.Models;

namespace Wayd.Common.Application.Tests.Sut.Employees.Queries;

public sealed class GetDirectReportsQueryHandlerTests
{
    private readonly FakeWaydDbContext _dbContext = new();

    [Fact]
    public async Task Handle_OrdersByLastNameThenFirstNameThenId()
    {
        // Arrange — two reports share a full name, so only the id separates them
        var managerId = Guid.NewGuid();
        var adams = NewReport(managerId, "Zoe", "Adams");
        var bakerAnn = NewReport(managerId, "Ann", "Baker");
        var toms = new[] { NewReport(managerId, "Tom", "Baker"), NewReport(managerId, "Tom", "Baker") }
            .OrderBy(e => e.Id).ToArray();
        foreach (var employee in new[] { toms[1], bakerAnn, adams, toms[0] })
            _dbContext.AddEmployee(employee);

        var handler = new GetDirectReportsQueryHandler(_dbContext, NullLogger<GetDirectReportsQueryHandler>.Instance);

        // Act
        var result = await handler.Handle(new GetDirectReportsQuery(managerId), TestContext.Current.CancellationToken);

        // Assert
        result.Select(e => e.Id).Should().Equal(adams.Id, bakerAnn.Id, toms[0].Id, toms[1].Id);
    }

    private static Wayd.Common.Domain.Employees.Employee NewReport(Guid managerId, string firstName, string lastName) =>
        new EmployeeFaker()
            .WithName(new PersonName(firstName, null, lastName))
            .WithManagerId(managerId)
            .Generate();
}
