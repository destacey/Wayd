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

    private static ImportEmployeeDto Employee(string number, string firstName, string lastName, string email, string? managerNumber = null, string[]? additionalEmails = null) =>
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
            ManagerNumber: managerNumber,
            AdditionalEmails: additionalEmails is null ? null : [.. additionalEmails.Select(e => new EmailAddress(e))]);

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

    /// <summary>
    /// Round-trips the work-email collection through the real provider: the value converter on
    /// <c>EmployeeEmails.Email</c>, the FK, and the unique index only exist against a real database.
    /// </summary>
    [Fact]
    public async Task Handle_PersistsWorkEmails_AndReadsThemBack()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        var command = new ImportEmployeesCommand(
        [
            Employee("E-3001", "Avery", "Chen", "avery.chen@acme.example",
                additionalEmails: ["avery.chen@acme-legacy.example"]),
        ]);

        // Act
        await using var handlerContext = _fixture.CreateContext();
        var result = await CreateHandler(handlerContext).Handle(command, cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        await using var assertContext = _fixture.CreateContext();
        var employee = await assertContext.Employees
            .Include(e => e.Emails)
            .SingleAsync(e => e.EmployeeNumber == "E-3001", cancellationToken);

        employee.Emails.Select(e => e.Email.Value).Should().BeEquivalentTo(
            ["avery.chen@acme.example", "avery.chen@acme-legacy.example"]);
        employee.Emails.Single(e => e.IsPrimary).Email.Value.Should().Be("avery.chen@acme.example");
    }

    /// <summary>
    /// The unique index on <c>EmployeeEmails.Email</c> spans the whole table, so two people cannot claim
    /// the same address. The command validator rejects this before SaveChanges — this pins the database
    /// constraint that backs it, so the two cannot silently drift apart.
    /// </summary>
    [Fact]
    public async Task Handle_TwoEmployeesClaimingTheSameAddress_ViolatesTheUniqueIndex()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        var command = new ImportEmployeesCommand(
        [
            Employee("E-3002", "Jordan", "Blake", "jordan@acme.example",
                additionalEmails: ["shared@acme-legacy.example"]),
            Employee("E-3003", "Sam", "Ortiz", "sam@acme.example",
                additionalEmails: ["shared@acme-legacy.example"]),
        ]);

        // Act
        await using var handlerContext = _fixture.CreateContext();
        var result = await CreateHandler(handlerContext).Handle(command, cancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue("the unique index rejects a duplicate address");

        await using var assertContext = _fixture.CreateContext();
        (await assertContext.Employees.CountAsync(cancellationToken)).Should().Be(0, "the batch rolls back");
    }

    /// <summary>
    /// Deleting an employee takes their addresses with them — the FK is configured to cascade, which only
    /// the real provider enforces.
    /// </summary>
    [Fact]
    public async Task DeletingAnEmployee_CascadesToTheirWorkEmails()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        var command = new ImportEmployeesCommand(
        [
            Employee("E-3004", "Riley", "Nakamura", "riley@acme.example",
                additionalEmails: ["riley@acme-legacy.example"]),
        ]);

        await using (var handlerContext = _fixture.CreateContext())
        {
            var result = await CreateHandler(handlerContext).Handle(command, cancellationToken);
            result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        }

        // Act — hard delete, bypassing the soft-delete interceptor, to exercise the FK itself.
        await using (var deleteContext = _fixture.CreateContext())
        {
            await deleteContext.Database.ExecuteSqlRawAsync(
                "DELETE FROM Organization.Employees WHERE EmployeeNumber = 'E-3004'", cancellationToken);
        }

        // Assert
        await using var assertContext = _fixture.CreateContext();
        var remaining = await assertContext.Set<Wayd.Common.Domain.Employees.EmployeeEmail>()
            .CountAsync(cancellationToken);
        remaining.Should().Be(0);
    }
}
