using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.IntegrationTests.Sut.Notifications;
using Wayd.Infrastructure.Migrators.MSSQL.Migrations;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the Move-Role-Notification-Reasons migration turns every stored Participant reason into Role and
/// leaves every other reason alone.
/// </summary>
/// <remarks>
/// The bit arithmetic is hand-written T-SQL, and the mute table's key can reject it, so only a real SQL Server
/// shows it does what it says.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class MoveRoleNotificationReasonsMigrationTests(SqlServerDbContextFixture fixture)
{
    private const string GadgetType = "test.move-role-reasons.gadget";
    private const int ParticipantValue = 4;
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 10, 27, 12, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Up_ShouldMoveParticipantToRole_AndKeepEveryOtherReason()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var employee = await NotificationTestData.SeedEmployee(_fixture);
        var both = await SeedNotification(employee.Id, NotificationReason.Following | NotificationReason.Participant, ct);
        var participantOnly = await SeedNotification(employee.Id, NotificationReason.Participant, ct);
        var followingOnly = await SeedNotification(employee.Id, NotificationReason.Following, ct);

        // Act
        await Run(MoveRoleNotificationReasons.UpSql, ct);

        // Assert
        (await ReasonsOf(both, ct)).Should().Be(NotificationReason.Following | NotificationReason.Role);
        (await ReasonsOf(participantOnly, ct)).Should().Be(NotificationReason.Role);
        (await ReasonsOf(followingOnly, ct)).Should().Be(NotificationReason.Following);
    }

    [Fact]
    public async Task Up_ShouldMoveAParticipantMuteToRole()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var employee = await NotificationTestData.SeedEmployee(_fixture);
        await SeedParticipantMute(employee.Id, ct);
        await SeedMute(employee.Id, NotificationReason.Following, ct);

        // Act
        await Run(MoveRoleNotificationReasons.UpSql, ct);

        // Assert
        (await MutesOf(employee.Id, ct)).Should().BeEquivalentTo([NotificationReason.Following, NotificationReason.Role]);
    }

    [Fact]
    public async Task Up_ShouldKeepOneMute_ForSomeoneWhoAlreadyMutedRole()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var employee = await NotificationTestData.SeedEmployee(_fixture);
        await SeedParticipantMute(employee.Id, ct);
        await SeedMute(employee.Id, NotificationReason.Role, ct);

        // Act
        await Run(MoveRoleNotificationReasons.UpSql, ct);

        // Assert
        (await MutesOf(employee.Id, ct)).Should().Equal(NotificationReason.Role);
    }

    [Fact]
    public async Task Up_ShouldChangeNothing_WhenRunAgain()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var employee = await NotificationTestData.SeedEmployee(_fixture);
        var notification = await SeedNotification(employee.Id, NotificationReason.Following | NotificationReason.Participant, ct);
        await SeedParticipantMute(employee.Id, ct);
        await Run(MoveRoleNotificationReasons.UpSql, ct);

        // Act
        await Run(MoveRoleNotificationReasons.UpSql, ct);

        // Assert
        (await ReasonsOf(notification, ct)).Should().Be(NotificationReason.Following | NotificationReason.Role);
        (await MutesOf(employee.Id, ct)).Should().Equal(NotificationReason.Role);
    }

    [Fact]
    public async Task Down_ShouldMoveRoleBackToParticipant()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var employee = await NotificationTestData.SeedEmployee(_fixture);
        var notification = await SeedNotification(employee.Id, NotificationReason.Following | NotificationReason.Participant, ct);
        await SeedParticipantMute(employee.Id, ct);
        await Run(MoveRoleNotificationReasons.UpSql, ct);

        // Act
        await Run(MoveRoleNotificationReasons.DownSql, ct);

        // Assert
        (await ReasonsOf(notification, ct)).Should().Be(NotificationReason.Following | NotificationReason.Participant);
        (await MutesOf(employee.Id, ct)).Should().Equal(NotificationReason.Participant);
    }

    private async Task<long> SeedNotification(Guid recipientId, NotificationReason reasons, CancellationToken ct)
    {
        FollowOwners.Register(Gadget);
        var notification = Notification.Create(
            recipientId, GadgetType, Guid.CreateVersion7(), Guid.CreateVersion7(), NotificationEvent.StatusChanged,
            EventActorKind.User, null, Timestamp, reasons).Value;
        await using var context = _fixture.CreateContext();
        context.Notifications.Add(notification);
        await context.SaveChangesAsync(ct);
        return notification.Id;
    }

    private async Task SeedMute(Guid employeeId, NotificationReason reason, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        context.ReasonMutes.Add(ReasonMute.Create(employeeId, reason, Timestamp).Value);
        await context.SaveChangesAsync(ct);
    }

    /// <summary>A mute of Participant as one was stored before the migration; the domain no longer allows it.</summary>
    private async Task SeedParticipantMute(Guid employeeId, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO [Notifications].[ReasonMutes] ([EmployeeId], [Reason], [MutedOn]) VALUES ({employeeId}, {ParticipantValue}, SYSUTCDATETIME())",
            ct);
    }

    private async Task Run(string sql, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        await context.Database.ExecuteSqlRawAsync(sql, ct);
    }

    private async Task<NotificationReason> ReasonsOf(long notificationId, CancellationToken ct)
    {
        await using var reader = _fixture.CreateContext();
        return await reader.Notifications.AsNoTracking()
            .Where(n => n.Id == notificationId)
            .Select(n => n.Reasons)
            .SingleAsync(ct);
    }

    private async Task<List<NotificationReason>> MutesOf(Guid employeeId, CancellationToken ct)
    {
        await using var reader = _fixture.CreateContext();
        return await reader.ReasonMutes.AsNoTracking()
            .Where(m => m.EmployeeId == employeeId)
            .Select(m => m.Reason)
            .ToListAsync(ct);
    }
}
