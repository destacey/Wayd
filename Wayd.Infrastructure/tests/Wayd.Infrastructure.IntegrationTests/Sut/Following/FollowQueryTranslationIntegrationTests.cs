using Microsoft.EntityFrameworkCore;
using Moq;
using NodaTime;
using Wayd.Common.Application.Records;
using Wayd.Common.Application.Following.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Following;
using Wayd.Common.Models;
using Wayd.Infrastructure.Following;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Planning.Application.PlanningIntervals;
using Wayd.Planning.Domain;
using Wayd.ProjectPortfolioManagement.Application.Programs;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Domain;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Following;

/// <summary>
/// Runs the Following list and every real read rule against SQL Server.
/// </summary>
/// <remarks>
/// The unit tests prove what the list returns over in-memory sets, where any LINQ runs. Only a real
/// provider proves the shape the scaling argument rests on — the caller's follows joined to each type's
/// readable ids, counted and paged in SQL — and that each read rule's projection translates at all, which
/// an in-memory set never checks.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class FollowQueryTranslationIntegrationTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 10, 23, 9, 30);

    [Fact]
    public async Task GetMyFollows_ShouldTranslate_AndLeaveOutFollowsOfRecordsThatDoNotExist()
    {
        // Arrange — follows of every real owner type, pointing at records that do not exist, as a
        // follow of a hard-deleted record would.
        var cancellationToken = TestContext.Current.CancellationToken;
        PpmFollowOwners.Register();
        PlanningFollowOwners.Register();
        var employee = await SeedEmployee();
        await using (var seed = fixture.CreateContext())
        {
            var store = new FollowStore(seed);
            foreach (var descriptor in PpmFollowOwners.All.Concat(PlanningFollowOwners.All))
            {
                await store.Add(Follow.Create(descriptor.Key, Guid.CreateVersion7(), employee.Id, Timestamp).Value, cancellationToken);
            }
        }

        await using var context = fixture.CreateContext();
        var principal = Principal(employee.Id);
        var handler = new GetMyFollowsQueryHandler(context, ReadRules(context, principal.Object), principal.Object);

        // Act
        var result = await handler.Handle(new GetMyFollowsQuery(PageSize: 5), cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task Targets_ShouldTranslateTheirReadAndDescribeQueries()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = fixture.CreateContext();
        var principal = Principal(Guid.CreateVersion7());

        foreach (var readRule in ReadRules(context, principal.Object))
        {
            // Act
            var readable = await readRule.ReadableIds(cancellationToken);
            var exists = await readable!.AnyAsync(id => id == Guid.CreateVersion7(), cancellationToken);
            var summaries = await readRule.Describe([Guid.CreateVersion7()], cancellationToken);

            // Assert
            exists.Should().BeFalse(readRule.OwnerType);
            summaries.Should().BeEmpty(readRule.OwnerType);
        }
    }

    [Fact]
    public async Task GetMyFollows_ShouldCountAndPageReadableFollowsInSql()
    {
        // Arrange — an owner type whose records are employees, so real rows exist to be read without
        // building a project hierarchy; two of the three followed are readable.
        var cancellationToken = TestContext.Current.CancellationToken;
        FollowOwners.Register(EmployeeOwner);
        var follower = await SeedEmployee();
        var newest = await SeedEmployee();
        var middle = await SeedEmployee();
        var unreadable = await SeedEmployee();
        await using (var seed = fixture.CreateContext())
        {
            var store = new FollowStore(seed);
            await store.Add(Follow.Create(EmployeeOwner.Key, middle.Id, follower.Id, Timestamp).Value, cancellationToken);
            await store.Add(Follow.Create(EmployeeOwner.Key, unreadable.Id, follower.Id, Timestamp.Plus(Duration.FromHours(1))).Value, cancellationToken);
            await store.Add(Follow.Create(EmployeeOwner.Key, newest.Id, follower.Id, Timestamp.Plus(Duration.FromHours(2))).Value, cancellationToken);
        }

        await using var context = fixture.CreateContext();
        var principal = Principal(follower.Id);
        var readRule = new EmployeeFollowTarget(context, readable: [newest.Id, middle.Id]);
        var handler = new GetMyFollowsQueryHandler(context, [readRule], principal.Object);

        // Act
        var firstPage = await handler.Handle(new GetMyFollowsQuery(EmployeeOwner.Key, PageNumber: 1, PageSize: 1), cancellationToken);
        var secondPage = await handler.Handle(new GetMyFollowsQuery(EmployeeOwner.Key, PageNumber: 2, PageSize: 1), cancellationToken);

        // Assert
        var first = firstPage.Value.Should().ContainSingle().Subject.Follows;
        first.TotalCount.Should().Be(2);
        first.Items.Should().ContainSingle().Which.Id.Should().Be(newest.Id);
        secondPage.Value.Should().ContainSingle().Subject.Follows.Items.Should().ContainSingle().Which.Id.Should().Be(middle.Id);
    }

    private static readonly FollowOwnerDescriptor EmployeeOwner = new("test.follow-translation.employee", "Employee");

    /// <summary>A read rule over real employee rows, readable only where the test says.</summary>
    private sealed class EmployeeFollowTarget(WaydDbContext context, Guid[] readable) : IRecordReadRule
    {
        public string OwnerType => EmployeeOwner.Key;

        public Task<IQueryable<Guid>?> ReadableIds(CancellationToken cancellationToken) =>
            Task.FromResult<IQueryable<Guid>?>(context.Employees.Where(e => readable.Contains(e.Id)).Select(e => e.Id));

        public async Task<IReadOnlyDictionary<Guid, RecordSummary>> Describe(
            IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            await context.Employees
                .Where(e => ids.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id, e => new RecordSummary(e.Key.ToString(), e.Name.FullName), cancellationToken);
    }

    private static IRecordReadRule[] ReadRules(WaydDbContext context, ICurrentPrincipal principal) =>
    [
        new ProjectReadRule(context, principal),
        new ProgramReadRule(context, principal),
        new PlanningIntervalObjectiveReadRule(context, principal),
    ];

    private static Mock<ICurrentPrincipal> Principal(Guid employeeId)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        return principal;
    }

    private async Task<Employee> SeedEmployee()
    {
        var suffix = $"{Guid.CreateVersion7():N}"[..12];
        var employee = Employee.Create(
            new PersonName("Avery", null, "Chen"),
            suffix,
            Timestamp,
            new EmailAddress($"{suffix}@acme.example"),
            jobTitle: null,
            department: null,
            officeLocation: null,
            managerId: null,
            isActive: true,
            employeeType: null,
            EventActor.System,
            Timestamp);

        await using var context = fixture.CreateContext();
        context.Employees.Add(employee);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return employee;
    }
}
