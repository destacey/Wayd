using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Domain.Activities;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.IntegrationTests.Sut.Notifications;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the Backfill-Message-Notification-Event-Ids migration points a posted message's notifications at the
/// message, and leaves every other notification alone.
/// </summary>
/// <remarks>
/// The join across schemas is hand-written T-SQL, so only a real SQL Server shows it does what it says.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class BackfillMessageNotificationEventIdsMigrationTests(SqlServerDbContextFixture fixture)
{
    private const string GadgetType = "test.backfill-message-event-ids.gadget";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 11, 20, 12, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Up_ShouldPointAPostedMessagesNotificationAtTheMessage()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var employee = await NotificationTestData.SeedEmployee(_fixture);
        var messageId = Guid.CreateVersion7();
        var eventId = Guid.CreateVersion7();
        await SeedActivity(eventId, "DiscussionMessagePostedEvent", "DiscussionMessage", messageId, ct);
        var notification = await SeedNotification(employee.Id, eventId, NotificationEvent.MessagePosted, ct);

        // Act
        await Run(BackfillMessageNotificationEventIds.UpSql, ct);

        // Assert
        (await EventIdOf(notification, ct)).Should().Be(messageId);
    }

    [Fact]
    public async Task Up_ShouldLeaveANotificationAboutAnyOtherEventAlone()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var employee = await NotificationTestData.SeedEmployee(_fixture);
        var eventId = Guid.CreateVersion7();
        await SeedActivity(eventId, "ProjectStatusChangedEvent", "Project", Guid.CreateVersion7(), ct);
        var notification = await SeedNotification(employee.Id, eventId, NotificationEvent.StatusChanged, ct);

        // Act
        await Run(BackfillMessageNotificationEventIds.UpSql, ct);

        // Assert
        (await EventIdOf(notification, ct)).Should().Be(eventId);
    }

    [Fact]
    public async Task Down_ShouldPointTheNotificationBackAtItsEvent()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var employee = await NotificationTestData.SeedEmployee(_fixture);
        var eventId = Guid.CreateVersion7();
        await SeedActivity(eventId, "DiscussionMessagePostedEvent", "DiscussionMessage", Guid.CreateVersion7(), ct);
        var notification = await SeedNotification(employee.Id, eventId, NotificationEvent.MessagePosted, ct);
        await Run(BackfillMessageNotificationEventIds.UpSql, ct);

        // Act
        await Run(BackfillMessageNotificationEventIds.DownSql, ct);

        // Assert
        (await EventIdOf(notification, ct)).Should().Be(eventId);
    }

    private async Task SeedActivity(Guid eventId, string eventType, string aggregateType, Guid aggregateId, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        context.ActivityLogs.Add(new ActivityLogEntry(
            eventId, eventType, ActivityCategory.Created, "Test", aggregateType, aggregateId,
            EventActor.System, Timestamp, 0, null, "{}"));
        await context.SaveChangesAsync(ct);
    }

    private async Task<long> SeedNotification(Guid recipientId, Guid eventId, NotificationEvent @event, CancellationToken ct)
    {
        FollowOwners.Register(Gadget);
        var notification = Notification.Create(
            recipientId, GadgetType, Guid.CreateVersion7(), eventId, @event,
            EventActorKind.User, null, Timestamp, NotificationReason.Mentioned).Value;
        await using var context = _fixture.CreateContext();
        context.Notifications.Add(notification);
        await context.SaveChangesAsync(ct);
        return notification.Id;
    }

    private async Task Run(string sql, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync(sql, ct);
    }

    private async Task<Guid> EventIdOf(long notificationId, CancellationToken ct)
    {
        await using var reader = _fixture.CreateContext();
        return await reader.Notifications.AsNoTracking()
            .Where(n => n.Id == notificationId)
            .Select(n => n.EventId)
            .SingleAsync(ct);
    }
}
