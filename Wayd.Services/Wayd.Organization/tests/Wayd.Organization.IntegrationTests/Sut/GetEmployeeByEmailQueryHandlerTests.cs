using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Employees.Dtos;
using Wayd.Common.Application.Employees.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Models;
using Wayd.Organization.IntegrationTests.Infrastructure;

namespace Wayd.Organization.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="GetEmployeeByEmailQueryHandler"/> against a real SQL Server container.
///
/// <para>
/// <c>Employee.Email</c> is mapped with a value converter (<c>EmailAddress</c> → <c>nvarchar</c>), not as a
/// complex property, so EF can translate the property as a whole but has no mapping for its <c>.Value</c>
/// sub-member. A filter written as <c>e.Email.Value == ...</c> therefore throws
/// <see cref="InvalidOperationException"/> ("could not be translated") at query-compilation time — on the
/// login path, since <c>UserService.GetEmployeeIdByEmail</c> dispatches this query during token exchange.
/// </para>
/// <para>
/// This class of failure is invisible to the in-memory <c>FakeWaydDbContext</c>, which runs LINQ-to-Objects
/// where <c>.Value</c> is an ordinary property read that evaluates fine. Only the production EF provider
/// compiles the expression to SQL, which is why these tests need the container fixture.
/// </para>
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetEmployeeByEmailQueryHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public GetEmployeeByEmailQueryHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    private const string SeededEmail = "ada.lovelace@acme.example";

    /// <summary>
    /// Seeds one employee through the real import handler, so the row is written by the production
    /// persistence path (converter included) rather than hand-inserted.
    /// </summary>
    private async Task<Guid> SeedEmployee(string email, CancellationToken cancellationToken)
    {
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(SqlServerDbContextFixture.FixedNow);

        await using var context = _fixture.CreateContext();
        var command = new ImportEmployeesCommand(
        [
            new ImportEmployeeDto(
                "E-4001",
                "Ada",
                null,
                "Lovelace",
                new EmailAddress(email),
                HireDate: SqlServerDbContextFixture.FixedNow,
                JobTitle: "Engineer",
                Department: "Engineering",
                OfficeLocation: null,
                ManagerNumber: null),
        ]);

        var result = await new ImportEmployeesCommandHandler(
            context,
            dateTimeProvider.Object,
            NullLogger<ImportEmployeesCommandHandler>.Instance).Handle(command, cancellationToken);

        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);

        await using var readContext = _fixture.CreateContext();
        return readContext.Employees.Single(e => e.EmployeeNumber == "E-4001").Id;
    }

    [Fact]
    public async Task Handle_ReturnsTheEmployeeId_WhenTheEmailMatchesExactly()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var expectedId = await SeedEmployee(SeededEmail, cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = new GetEmployeeByEmailQueryHandler(context);

        // Act
        var employeeId = await handler.Handle(new GetEmployeeByEmailQuery(SeededEmail), cancellationToken);

        // Assert
        employeeId.Should().Be(expectedId);
    }

    [Fact]
    public async Task Handle_ReturnsTheEmployeeId_WhenTheEmailCasingDiffersFromTheStoredValue()
    {
        // Arrange — the handler documents this lookup as case-insensitive. Nothing in the LINQ folds case, so
        // this pins the behaviour the doc promises (today it rests on the database's CI collation). Querying
        // with different casing than was seeded also stops a client-evaluation "fix" from passing by accident.
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var expectedId = await SeedEmployee(SeededEmail, cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = new GetEmployeeByEmailQueryHandler(context);

        // Act
        var employeeId = await handler.Handle(
            new GetEmployeeByEmailQuery(SeededEmail.ToUpperInvariant()), cancellationToken);

        // Assert
        employeeId.Should().Be(expectedId);
    }

    /// <summary>
    /// Pins the email projection <c>UserService.UpdateMissingEmployeeIds</c> runs (the PeopleSync
    /// User↔Employee backfill). It shares this query's converter hazard: projecting <c>e.Email.Value</c>
    /// instead of <c>e.Email</c> throws "could not be translated" and fails every people sync. The method
    /// itself needs UserManager/Identity plumbing this fixture doesn't host, so this covers the EF shape
    /// only — the part that can't be caught without a real provider.
    /// </summary>
    [Fact]
    public async Task EmployeeEmailProjection_UsedByUpdateMissingEmployeeIds_Translates()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        var expectedId = await SeedEmployee(SeededEmail, cancellationToken);

        await using var context = _fixture.CreateContext();

        // Act
        var employees = await context.Employees
            .Select(e => new { e.Id, e.Email })
            .ToListAsync(cancellationToken);

        // Assert
        var employeeIdByEmail = employees
            .GroupBy(e => e.Email.Value, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Last().Id, StringComparer.OrdinalIgnoreCase);

        employeeIdByEmail.Should().ContainKey(SeededEmail.ToUpperInvariant())
            .WhoseValue.Should().Be(expectedId);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenNoEmployeeHasThatEmail()
    {
        // Arrange — no seeding: the translation failure happens at query-compilation time, before any row is
        // read, so an empty table is enough to reproduce the login-path exception.
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = new GetEmployeeByEmailQueryHandler(context);

        // Act
        var employeeId = await handler.Handle(
            new GetEmployeeByEmailQuery("nobody@acme.example"), cancellationToken);

        // Assert — null specifically, not Guid.Empty: the RequireEmployeeRecord registration gate in
        // UserService checks HasValue, which an empty Guid would satisfy.
        employeeId.Should().BeNull();
    }
}
