using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="ImportEmployeesCommandHandler"/> against a real SQL Server container.
/// Employees are pervasive NodaTime carriers (<c>HireDate</c>, system-audit columns) and the manager-linkage
/// pass runs a real <c>IN</c> query over the container, so this exercises the production provider end-to-end.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class ImportEmployeesCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public ImportEmployeesCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    private static ImportEmployeesCommandHandler CreateHandler(Wayd.Infrastructure.Persistence.Context.WaydDbContext context)
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);

        return new ImportEmployeesCommandHandler(context, dateTimeProvider.Object, NullLogger<ImportEmployeesCommandHandler>.Instance);
    }

    private static ImportEmployeeDto Employee(string number, string firstName, string lastName, string email, string? managerNumber = null) =>
        new(
            number,
            firstName,
            null,
            lastName,
            new EmailAddress(email),
            HireDate: SqlServerDbContextFixture.FixedNow,
            JobTitle: "Engineer",
            Department: "Engineering",
            OfficeLocation: null,
            ManagerNumber: managerNumber);

    [Fact]
    public async Task Handle_ImportsEmployees_AndResolvesManagerLinkageAcrossTheBatch()
    {
        // Arrange — the report appears before its manager, so linkage must resolve regardless of row order.
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        var command = new ImportEmployeesCommand(
        [
            Employee("E-1001", "Ada", "Lovelace", "ada@acme.example", managerNumber: "E-2001"),
            Employee("E-2001", "Grace", "Hopper", "grace@acme.example"),
        ]);

        // Act
        await using var handlerContext = _fixture.CreateContext();
        var result = await CreateHandler(handlerContext).Handle(command, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        await using var assertContext = _fixture.CreateContext();
        var employees = await assertContext.Employees.ToListAsync(cancellationToken);
        employees.Should().HaveCount(2);

        var manager = employees.Single(e => e.EmployeeNumber == "E-2001");
        var report = employees.Single(e => e.EmployeeNumber == "E-1001");
        report.ManagerId.Should().Be(manager.Id);
        manager.ManagerId.Should().BeNull();
    }
}
