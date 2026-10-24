using Microsoft.EntityFrameworkCore;
using Moq;
using NodaTime;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Following;
using Wayd.Common.Application.Following.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Activities;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums;
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
/// Runs the Following queries and every real read rule against SQL Server.
/// </summary>
/// <remarks>
/// The unit tests prove what the queries return over in-memory sets, where any LINQ runs. Only a real
/// provider proves what the design rests on: each owner type's follows joined to its readable records and
/// combined with <c>UNION ALL</c>, then filtered, counted and paged in SQL — and that every read rule's
/// projection translates at all. The real owner types are always part of the union here, so a column whose
/// store type differs between them fails these tests.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class FollowQueryTranslationIntegrationTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 10, 23, 9, 30);
    private static readonly FollowOwnerDescriptor EmployeeOwner = new("test.follow-translation.employee", "Test Employee");

    [Fact]
    public async Task GetMyFollows_ShouldFilterSortAndPageAcrossEveryOwnerTypeInSql()
    {
        // Arrange — real owner types (with follows of records that no longer exist) unioned with an owner
        // type over real employee rows, two of three readable, one of those unhealthy.
        var cancellationToken = TestContext.Current.CancellationToken;
        RegisterOwners();
        var follower = await SeedEmployee();
        var newest = await SeedEmployee();
        var middle = await SeedEmployee();
        var unreadable = await SeedEmployee();
        await using (var seed = fixture.CreateContext())
        {
            var store = new FollowStore(seed);
            foreach (var descriptor in PpmFollowOwners.All.Concat(PlanningFollowOwners.All))
            {
                await store.Add(Follow.Create(descriptor.Key, Guid.CreateVersion7(), follower.Id, Timestamp).Value, cancellationToken);
            }

            await store.Add(Follow.Create(EmployeeOwner.Key, middle.Id, follower.Id, Timestamp).Value, cancellationToken);
            await store.Add(Follow.Create(EmployeeOwner.Key, unreadable.Id, follower.Id, Timestamp.Plus(Duration.FromHours(1))).Value, cancellationToken);
            await store.Add(Follow.Create(EmployeeOwner.Key, newest.Id, follower.Id, Timestamp.Plus(Duration.FromHours(2))).Value, cancellationToken);
        }

        await using var context = fixture.CreateContext();
        var principal = Principal(follower.Id);
        var readRules = ReadRules(context, principal.Object, readable: [newest.Id, middle.Id], unhealthy: [middle.Id]);
        var handler = new GetMyFollowsQueryHandler(context, context, readRules, principal.Object, Clock());

        // Act
        var firstPage = await handler.Handle(new GetMyFollowsQuery(Sort: FollowSort.Followed, PageNumber: 1, PageSize: 1), cancellationToken);
        var secondPage = await handler.Handle(new GetMyFollowsQuery(Sort: FollowSort.Followed, PageNumber: 2, PageSize: 1), cancellationToken);
        var unhealthy = await handler.Handle(new GetMyFollowsQuery(View: FollowView.Unhealthy), cancellationToken);
        var byLastChanged = await handler.Handle(new GetMyFollowsQuery(View: FollowView.ChangedRecently), cancellationToken);
        var searched = await handler.Handle(new GetMyFollowsQuery(Search: "zzz-no-match"), cancellationToken);

        // Assert
        firstPage.IsSuccess.Should().BeTrue(firstPage.IsFailure ? firstPage.Error : null);
        firstPage.Value.TotalCount.Should().Be(2);
        firstPage.Value.Items.Should().ContainSingle().Which.Id.Should().Be(newest.Id);
        secondPage.Value.Items.Should().ContainSingle().Which.Id.Should().Be(middle.Id);
        unhealthy.Value.Items.Select(i => i.Id).Should().Equal(middle.Id);
        byLastChanged.Value.TotalCount.Should().Be(2, "each seeded employee's creation is recorded in the activity log");
        byLastChanged.Value.Items.Should().OnlyContain(i => i.LastChangedOn != null);
        searched.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task GetMyFollows_ShouldSortBothWays_OnTheRecordsOwnAndRelatedActivityOnly_InSql()
    {
        // Arrange — two followed employees. One has a later entry logged under another aggregate type that
        // shares its id, which must not count; the other is named as related by a later entry, which must.
        var cancellationToken = TestContext.Current.CancellationToken;
        RegisterOwners();
        var follower = await SeedEmployee();
        var shadowed = await SeedEmployee();
        var related = await SeedEmployee();
        var relatedOn = Instant.FromUtc(2099, 1, 1, 0, 0);
        await using (var seed = fixture.CreateContext())
        {
            var store = new FollowStore(seed);
            await store.Add(Follow.Create(EmployeeOwner.Key, shadowed.Id, follower.Id, Timestamp).Value, cancellationToken);
            await store.Add(Follow.Create(EmployeeOwner.Key, related.Id, follower.Id, Timestamp.Plus(Duration.FromHours(1))).Value, cancellationToken);

            seed.ActivityLogs.Add(new ActivityLogEntry(
                Guid.NewGuid(), "TestChangedEvent", ActivityCategory.Updated, "Test", "Project", shadowed.Id,
                EventActor.System, Instant.FromUtc(2099, 1, 2, 0, 0), 0, null, "{}"));
            seed.ActivityLogs.Add(new ActivityLogEntry(
                Guid.NewGuid(), "TestChangedEvent", ActivityCategory.Updated, "Test", "Team", Guid.CreateVersion7(),
                EventActor.System, relatedOn, 0, null, "{}",
                relatedAggregates: [new AggregateReference("Employee", related.Id)]));
            await seed.SaveChangesAsync(cancellationToken);
        }

        await using var context = fixture.CreateContext();
        var principal = Principal(follower.Id);
        var handler = new GetMyFollowsQueryHandler(
            context, context, ReadRules(context, principal.Object, readable: [shadowed.Id, related.Id], unhealthy: []), principal.Object, Clock());

        // Act
        var changedNewest = await handler.Handle(new GetMyFollowsQuery(), cancellationToken);
        var changedOldest = await handler.Handle(new GetMyFollowsQuery(Direction: SortDirection.Ascending), cancellationToken);
        var followedOldest = await handler.Handle(new GetMyFollowsQuery(Sort: FollowSort.Followed, Direction: SortDirection.Ascending), cancellationToken);

        // Assert
        changedNewest.IsSuccess.Should().BeTrue(changedNewest.IsFailure ? changedNewest.Error : null);
        changedNewest.Value.Items.Select(i => i.Id).Should().Equal(related.Id, shadowed.Id);
        changedNewest.Value.Items[0].LastChangedOn.Should().Be(relatedOn);
        changedNewest.Value.Items[1].LastChangedOn.Should().NotBeNull()
            .And.Subject.Should().Match<Instant?>(changed => changed < relatedOn, "the later entry under another type does not count");
        changedOldest.Value.Items.Select(i => i.Id).Should().Equal(shadowed.Id, related.Id);
        followedOldest.Value.Items.Select(i => i.Id).Should().Equal(shadowed.Id, related.Id);
    }

    [Fact]
    public async Task GetMyFollowCounts_ShouldCountEachTypeAndViewInSql()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        RegisterOwners();
        var follower = await SeedEmployee();
        var followed = await SeedEmployee();
        await using (var seed = fixture.CreateContext())
        {
            await new FollowStore(seed).Add(Follow.Create(EmployeeOwner.Key, followed.Id, follower.Id, Timestamp).Value, cancellationToken);
        }

        await using var context = fixture.CreateContext();
        var principal = Principal(follower.Id);
        var handler = new GetMyFollowCountsQueryHandler(
            context, context, ReadRules(context, principal.Object, readable: [followed.Id], unhealthy: [followed.Id]), principal.Object, Clock());

        // Act
        var counts = await handler.Handle(new GetMyFollowCountsQuery(), cancellationToken);

        // Assert
        counts.Total.Should().Be(1);
        counts.Types.Should().Contain(t => t.OwnerType == EmployeeOwner.Key && t.Count == 1);
        counts.Types.Where(t => t.OwnerType != EmployeeOwner.Key).Should().OnlyContain(t => t.Count == 0);
        counts.Views.Should().ContainSingle(v => v.View == FollowView.Unhealthy).Which.Count.Should().Be(1);
        counts.Views.Should().ContainSingle(v => v.View == FollowView.Closed).Which.Count.Should().Be(0);
    }

    [Fact]
    public async Task GetFollowedRecord_ShouldReadOneRecordAndItsActivityInSql()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        RegisterOwners();
        var follower = await SeedEmployee();
        var followed = await SeedEmployee();
        await using (var seed = fixture.CreateContext())
        {
            // Followed before the employee's creation was recorded, so that activity counts as since following.
            await new FollowStore(seed).Add(Follow.Create(EmployeeOwner.Key, followed.Id, follower.Id, Timestamp.Minus(Duration.FromDays(1))).Value, cancellationToken);
        }

        await using var context = fixture.CreateContext();
        var principal = Principal(follower.Id);
        var handler = new GetFollowedRecordQueryHandler(
            context, context, new ActivityLogReader(context), ReadRules(context, principal.Object, readable: [followed.Id], unhealthy: []), principal.Object);

        // Act
        var result = await handler.Handle(new GetFollowedRecordQuery(EmployeeOwner.Key, followed.Id), cancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().NotBeNull();
        result.Value!.Record.Id.Should().Be(followed.Id);
        result.Value.ChangesSinceFollowed.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ReadRules_ShouldTranslateTheirReadableSearchAndDescribeQueries()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        await using var context = fixture.CreateContext();
        var principal = Principal(Guid.CreateVersion7());

        foreach (var readRule in RealReadRules(context, principal.Object))
        {
            // Act
            var readable = await readRule.Readable(null, cancellationToken);
            var exists = await readable!.AnyAsync(r => r.Id == Guid.CreateVersion7(), cancellationToken);
            var byName = await (await readRule.Readable("no such name", cancellationToken))!.CountAsync(cancellationToken);
            var byKey = await (await readRule.Readable("42", cancellationToken))!.CountAsync(cancellationToken);
            var summaries = await readRule.Describe([Guid.CreateVersion7()], cancellationToken);

            // Assert
            exists.Should().BeFalse(readRule.OwnerType);
            byName.Should().Be(0, readRule.OwnerType);
            byKey.Should().BeGreaterThanOrEqualTo(0, readRule.OwnerType);
            summaries.Should().BeEmpty(readRule.OwnerType);
        }
    }

    private static void RegisterOwners()
    {
        PpmFollowOwners.Register();
        PlanningFollowOwners.Register();
        FollowOwners.Register(EmployeeOwner);
    }

    private static IRecordReadRule[] RealReadRules(WaydDbContext context, ICurrentPrincipal principal) =>
    [
        new ProjectReadRule(context, principal, Clock()),
        new ProgramReadRule(context, principal),
        new PlanningIntervalObjectiveReadRule(context, principal, Clock()),
    ];

    private static IRecordReadRule[] ReadRules(WaydDbContext context, ICurrentPrincipal principal, Guid[] readable, Guid[] unhealthy) =>
        [.. RealReadRules(context, principal), new EmployeeReadRule(context, readable, unhealthy)];

    private static IDateTimeProvider Clock()
    {
        var clock = new Mock<IDateTimeProvider>();
        // A day after the seeded activity, so it counts as changed recently.
        clock.Setup(c => c.Now).Returns(Timestamp.Plus(Duration.FromDays(1)));
        return clock.Object;
    }

    private static Mock<ICurrentPrincipal> Principal(Guid employeeId)
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.HasPermission(It.IsAny<string>(), It.IsAny<CancellationToken>())).ReturnsAsync(true);
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(employeeId);
        return principal;
    }

    /// <summary>A read rule over real employee rows, readable and unhealthy only where the test says.</summary>
    private sealed class EmployeeReadRule(WaydDbContext context, Guid[] readable, Guid[] unhealthy) : IRecordReadRule
    {
        public string OwnerType => EmployeeOwner.Key;

        public string ActivityAggregateType => "Employee";

        public Task<IQueryable<RecordFacts>?> Readable(string? search, CancellationToken cancellationToken)
        {
            var employees = context.Employees.Where(e => readable.Contains(e.Id));
            if (search is not null)
            {
                employees = employees.Where(e => e.EmployeeNumber.Contains(search));
            }

            return Task.FromResult<IQueryable<RecordFacts>?>(employees.Select(e => new RecordFacts
            {
                Id = e.Id,
                IsUnhealthy = unhealthy.Contains(e.Id),
                IsClosed = false,
            }));
        }

        public async Task<IReadOnlyDictionary<Guid, RecordSummary>> Describe(
            IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
            await context.Employees
                .Where(e => ids.Contains(e.Id))
                .ToDictionaryAsync(
                    e => e.Id,
                    e => new RecordSummary
                    {
                        Key = e.Key.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        Name = e.Name.FullName,
                        StatusName = "Active",
                        Lifecycle = LifecycleCategory.Active,
                    },
                    cancellationToken);
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
