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
/// <para>
/// These need the container: <c>Employee.Email</c> is mapped with a value converter, so filtering on
/// <c>e.Email.Value</c> throws "could not be translated" — but only under a real provider. The in-memory
/// <c>FakeWaydDbContext</c> runs LINQ-to-Objects, where <c>.Value</c> evaluates fine and the same test
/// passes against broken code. This query runs on the login path via <c>UserService</c>.
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
    /// Seeds through the real import handler so the row is written by the production persistence path,
    /// converter included, rather than hand-inserted in a shape the app would never produce.
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
        // Arrange — the handler's doc promises case-insensitive lookup, but nothing in the LINQ folds case:
        // it rests entirely on the database's CI collation, so a collation change would break it silently.
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

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("missing@tld")]
    public async Task Handle_ReturnsNull_WhenTheEmailIsMalformed(string email)
    {
        // Arrange — this runs on the sign-in path against an identity-provider claim we don't control, so a
        // malformed value must be "no match". Constructing EmailAddress from it would throw and 500 the login.
        var cancellationToken = TestContext.Current.CancellationToken;
        await _fixture.ResetOrganizationData(cancellationToken);
        await SeedEmployee(SeededEmail, cancellationToken);

        await using var context = _fixture.CreateContext();
        var handler = new GetEmployeeByEmailQueryHandler(context);

        // Act
        var employeeId = await handler.Handle(new GetEmployeeByEmailQuery(email), cancellationToken);

        // Assert
        employeeId.Should().BeNull();
    }

    /// <summary>
    /// Pins the email projection in <c>UserService.UpdateMissingEmployeeIds</c>, which carried the same
    /// converter hazard and failed every PeopleSync run. Covers the EF shape only — the method itself needs
    /// UserManager plumbing this fixture doesn't host.
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
