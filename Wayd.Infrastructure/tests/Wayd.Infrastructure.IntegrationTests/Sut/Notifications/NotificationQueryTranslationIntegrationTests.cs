using Microsoft.EntityFrameworkCore;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Notifications.Queries;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Notifications;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Planning.Application.PlanningIntervals;
using Wayd.Planning.Domain;
using Wayd.ProjectPortfolioManagement.Application.Programs;
using Wayd.ProjectPortfolioManagement.Application.Projects;
using Wayd.ProjectPortfolioManagement.Domain;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Notifications;

/// <summary>
/// Runs the notification queries and every real read rule against SQL Server.
/// </summary>
/// <remarks>
/// The unit tests prove what the queries return over in-memory sets. Only a real provider proves that each
/// owner type's notifications joined to its readable records, combined with <c>UNION ALL</c>, then grouped by
/// record, counted and paged by cursor, translates at all. The real owner types are always part of the union here, so a
/// column whose store type differs between them fails these tests.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class NotificationQueryTranslationIntegrationTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 10, 24, 12, 0);
    private static readonly FollowOwnerDescriptor EmployeeOwner = new("test.notification-translation.employee", "Test Employee");
    private static readonly FollowOwnerDescriptor ColleagueOwner = new("test.notification-translation.colleague", "Test Colleague");

    [Fact]
    public async Task GetMyNotifications_ShouldGroupCountAndPageByCursorAcrossEveryOwnerTypeInSql()
    {
        // Arrange — notifications on every real owner type (about records that do not exist) unioned with an
        // owner type over real employee rows: two readable, one not. The middle record has two notifications,
        // one already read, for different reasons.
        var cancellationToken = TestContext.Current.CancellationToken;
        RegisterOwners();
        var recipient = await NotificationTestData.SeedEmployee(fixture);
        var actor = await NotificationTestData.SeedEmployee(fixture);
        var newest = await NotificationTestData.SeedEmployee(fixture);
        var middle = await NotificationTestData.SeedEmployee(fixture);
        var unreadable = await NotificationTestData.SeedEmployee(fixture);

        await using (var seed = fixture.CreateContext())
        {
            var store = new NotificationStore(seed);
            foreach (var descriptor in PpmFollowOwners.All.Concat(PlanningFollowOwners.All))
            {
                await Seed(store, recipient.Id, descriptor.Key, Guid.CreateVersion7(), Timestamp.Plus(Duration.FromDays(1)));
            }

            await Seed(store, recipient.Id, EmployeeOwner.Key, middle.Id, Timestamp, NotificationReason.Participant, actor.Id);
            await Seed(store, recipient.Id, EmployeeOwner.Key, middle.Id, Timestamp.Plus(Duration.FromHours(1)), NotificationReason.Following, actor.Id);
            await Seed(store, recipient.Id, EmployeeOwner.Key, unreadable.Id, Timestamp.Plus(Duration.FromHours(2)));
            await Seed(store, recipient.Id, EmployeeOwner.Key, newest.Id, Timestamp.Plus(Duration.FromHours(3)));

            var firstOnMiddle = await seed.Notifications
                .Where(n => n.RecipientEmployeeId == recipient.Id && n.OwnerId == middle.Id)
                .OrderBy(n => n.OccurredOn)
                .Select(n => n.Id)
                .FirstAsync(cancellationToken);
            await store.MarkRead(recipient.Id, firstOnMiddle, Timestamp, cancellationToken);
        }

        await using var context = fixture.CreateContext();
        var principal = Principal(recipient.Id);
        var handler = new GetMyNotificationsQueryHandler(context, context, ReadRules(context, principal.Object, readable: [newest.Id, middle.Id]), principal.Object);

        // Act
        var firstPage = await handler.Handle(new GetMyNotificationsQuery(PageSize: 1), cancellationToken);
        var secondPage = await handler.Handle(new GetMyNotificationsQuery(Before: firstPage.NextCursor, PageSize: 1), cancellationToken);
        var unreadOnly = await handler.Handle(new GetMyNotificationsQuery(UnreadOnly: true), cancellationToken);

        // Assert
        firstPage.Items.Should().ContainSingle().Which.Record.Id.Should().Be(newest.Id);
        firstPage.NextCursor.Should().NotBeNull();
        secondPage.NextCursor.Should().BeNull();

        var entry = secondPage.Items.Should().ContainSingle().Subject;
        entry.Record.Id.Should().Be(middle.Id);
        entry.Record.OwnerType.Should().Be(EmployeeOwner.Key);
        entry.Count.Should().Be(2);
        entry.UnreadCount.Should().Be(1);
        entry.OccurredOn.Should().Be(Timestamp.Plus(Duration.FromHours(1)));
        entry.Reasons.Should().BeEquivalentTo([NotificationReason.Following, NotificationReason.Participant]);
        entry.Actor.Should().NotBeNull();
        entry.Actor!.Id.Should().Be(actor.Id);

        unreadOnly.Items.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetMyNotifications_ShouldWalkEveryEntryOnceByCursor_WhenChangesDifferByATickOrTie()
    {
        // Arrange — two records a single tick apart, then three changed at one instant across two owner types,
        // so the cursor must survive the round trip through SQL Server to the tick and settle the tie by owner
        // type and then record id, compared in SQL as the page was ordered.
        var cancellationToken = TestContext.Current.CancellationToken;
        RegisterOwners();
        var recipient = await NotificationTestData.SeedEmployee(fixture);
        var records = new List<Guid>();
        for (var i = 0; i < 5; i++)
        {
            records.Add((await NotificationTestData.SeedEmployee(fixture)).Id);
        }

        await using (var seed = fixture.CreateContext())
        {
            var store = new NotificationStore(seed);
            await Seed(store, recipient.Id, EmployeeOwner.Key, records[0], Timestamp);
            await Seed(store, recipient.Id, EmployeeOwner.Key, records[1], Timestamp.PlusTicks(1));
            await Seed(store, recipient.Id, EmployeeOwner.Key, records[2], Timestamp.PlusTicks(2));
            await Seed(store, recipient.Id, ColleagueOwner.Key, records[3], Timestamp.PlusTicks(2));
            await Seed(store, recipient.Id, EmployeeOwner.Key, records[4], Timestamp.PlusTicks(2));
        }

        await using var context = fixture.CreateContext();
        var principal = Principal(recipient.Id);
        var handler = new GetMyNotificationsQueryHandler(context, context, ReadRules(context, principal.Object, readable: [.. records]), principal.Object);

        // Act
        var seen = new List<Guid>();
        string? before = null;
        do
        {
            var page = await handler.Handle(new GetMyNotificationsQuery(Before: before, PageSize: 1), cancellationToken);
            seen.AddRange(page.Items.Select(i => i.Record.Id));
            before = page.NextCursor;
        }
        while (before is not null && seen.Count <= records.Count);

        // Assert — newest first, each once. Of the three tied, the colleague sorts first by owner type; the two
        // employees follow in SQL Server's order for their ids.
        seen.Should().HaveCount(5).And.OnlyHaveUniqueItems();
        seen[0].Should().Be(records[3]);
        seen.Skip(1).Take(2).Should().BeEquivalentTo([records[2], records[4]]);
        seen.Skip(3).Should().Equal(records[1], records[0]);
    }

    [Fact]
    public async Task GetMyUnreadNotificationCount_ShouldCountUnreadRecordsAcrossEveryOwnerTypeInSql()
    {
        // Arrange — three unread notifications about one readable record count once; a fully read record and
        // an unreadable one do not count.
        var cancellationToken = TestContext.Current.CancellationToken;
        RegisterOwners();
        var recipient = await NotificationTestData.SeedEmployee(fixture);
        var busy = await NotificationTestData.SeedEmployee(fixture);
        var read = await NotificationTestData.SeedEmployee(fixture);
        var unreadable = await NotificationTestData.SeedEmployee(fixture);

        await using (var seed = fixture.CreateContext())
        {
            var store = new NotificationStore(seed);
            foreach (var descriptor in PpmFollowOwners.All.Concat(PlanningFollowOwners.All))
            {
                await Seed(store, recipient.Id, descriptor.Key, Guid.CreateVersion7(), Timestamp);
            }

            for (var i = 0; i < 3; i++)
            {
                await Seed(store, recipient.Id, EmployeeOwner.Key, busy.Id, Timestamp.Plus(Duration.FromMinutes(i)));
            }

            await Seed(store, recipient.Id, EmployeeOwner.Key, read.Id, Timestamp);
            await Seed(store, recipient.Id, EmployeeOwner.Key, unreadable.Id, Timestamp);
            await store.MarkRecordRead(recipient.Id, EmployeeOwner.Key, read.Id, Timestamp, cancellationToken);
        }

        await using var context = fixture.CreateContext();
        var principal = Principal(recipient.Id);
        var handler = new GetMyUnreadNotificationCountQueryHandler(context, ReadRules(context, principal.Object, readable: [busy.Id, read.Id]), principal.Object);

        // Act
        var count = await handler.Handle(new GetMyUnreadNotificationCountQuery(), cancellationToken);

        // Assert
        count.Count.Should().Be(1);
        count.IsCapped.Should().BeFalse();
    }

    private static async Task Seed(
        NotificationStore store,
        Guid recipientId,
        string ownerType,
        Guid ownerId,
        Instant occurredOn,
        NotificationReason reasons = NotificationReason.Following,
        Guid? actorEmployeeId = null)
    {
        var notification = Notification.Create(
            recipientId, ownerType, ownerId, Guid.CreateVersion7(), NotificationEvent.StatusChanged,
            EventActorKind.User, actorEmployeeId, occurredOn, reasons).Value;

        await store.Add([notification], TestContext.Current.CancellationToken);
    }

    private static void RegisterOwners()
    {
        PpmFollowOwners.Register();
        PlanningFollowOwners.Register();
        FollowOwners.Register(EmployeeOwner, ColleagueOwner);
    }

    private static IRecordReadRule[] ReadRules(WaydDbContext context, ICurrentPrincipal principal, Guid[] readable) =>
    [
        new ProjectReadRule(context, principal, Clock()),
        new ProgramReadRule(context, principal),
        new PlanningIntervalObjectiveReadRule(context, principal, Clock()),
        new EmployeeReadRule(context, readable, EmployeeOwner.Key),
        new EmployeeReadRule(context, readable, ColleagueOwner.Key),
    ];

    private static IDateTimeProvider Clock()
    {
        var clock = new Mock<IDateTimeProvider>();
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

    /// <summary>A read rule over real employee rows, readable only where the test says.</summary>
    private sealed class EmployeeReadRule(WaydDbContext context, Guid[] readable, string ownerType) : IRecordReadRule
    {
        public string OwnerType => ownerType;

        public string ActivityAggregateType => "Employee";

        public Task<IQueryable<RecordFacts>?> Readable(string? search, CancellationToken cancellationToken) =>
            Task.FromResult<IQueryable<RecordFacts>?>(context.Employees
                .Where(e => readable.Contains(e.Id))
                .Select(e => new RecordFacts { Id = e.Id, IsUnhealthy = false, IsClosed = false }));

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
}
