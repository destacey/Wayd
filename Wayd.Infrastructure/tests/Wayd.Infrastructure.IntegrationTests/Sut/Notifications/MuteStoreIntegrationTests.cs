using Microsoft.EntityFrameworkCore;
using NodaTime;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Notifications;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Notifications;

/// <summary>
/// The store against real SQL Server: what makes muting idempotent is each table's key, which no fake
/// enforces.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class MuteStoreIntegrationTests(SqlServerDbContextFixture fixture)
{
    private const string GadgetType = "test.mute-store.gadget";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly Instant Timestamp = Instant.FromUtc(2026, 10, 25, 9, 30);

    [Fact]
    public async Task AddRecordMute_ShouldSaveTheMute_AndReportAlreadyMutedAfterwards()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var employee = await NotificationTestData.SeedEmployee(fixture);
        var ownerId = Guid.CreateVersion7();
        await using (var first = fixture.CreateContext())
        {
            (await new MuteStore(first).Add(NewRecordMute(ownerId, employee.Id), cancellationToken)).Should().BeTrue();
        }

        await using var context = fixture.CreateContext();

        // Act
        var added = await new MuteStore(context).Add(
            NewRecordMute(ownerId, employee.Id, Timestamp.Plus(Duration.FromDays(1))), cancellationToken);

        // Assert
        added.Should().BeFalse();
        await using var reader = fixture.CreateContext();
        var stored = await reader.RecordMutes.Where(m => m.OwnerId == ownerId).ToListAsync(cancellationToken);
        stored.Should().ContainSingle().Which.MutedOn.Should().Be(Timestamp);
    }

    [Fact]
    public async Task AddRecordMute_ShouldLetExactlyOneOfManyConcurrentMutesWin()
    {
        // Arrange — each caller on its own context, so every one can pass the existence check first.
        var cancellationToken = TestContext.Current.CancellationToken;
        var employee = await NotificationTestData.SeedEmployee(fixture);
        var ownerId = Guid.CreateVersion7();
        var contexts = Enumerable.Range(0, 8).Select(_ => fixture.CreateContext()).ToList();

        try
        {
            // Act
            var outcomes = await Task.WhenAll(contexts.Select(c =>
                Task.Run(() => new MuteStore(c).Add(NewRecordMute(ownerId, employee.Id), cancellationToken), cancellationToken)));

            // Assert
            outcomes.Count(added => added).Should().Be(1);
            await using var reader = fixture.CreateContext();
            (await reader.RecordMutes.CountAsync(m => m.OwnerId == ownerId, cancellationToken)).Should().Be(1);
        }
        finally
        {
            foreach (var context in contexts)
            {
                await context.DisposeAsync();
            }
        }
    }

    [Fact]
    public async Task RemoveRecordMute_ShouldDeleteOnlyThatPersonsMuteOfThatRecord()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var avery = await NotificationTestData.SeedEmployee(fixture);
        var blake = await NotificationTestData.SeedEmployee(fixture);
        var gadgetA = Guid.CreateVersion7();
        var gadgetB = Guid.CreateVersion7();
        await using (var seed = fixture.CreateContext())
        {
            var seeder = new MuteStore(seed);
            await seeder.Add(NewRecordMute(gadgetA, avery.Id), cancellationToken);
            await seeder.Add(NewRecordMute(gadgetA, blake.Id), cancellationToken);
            await seeder.Add(NewRecordMute(gadgetB, avery.Id), cancellationToken);
        }

        await using var context = fixture.CreateContext();

        // Act
        var removed = await new MuteStore(context).RemoveRecordMute(GadgetType, gadgetA, avery.Id, cancellationToken);
        var removedAgain = await new MuteStore(context).RemoveRecordMute(GadgetType, gadgetA, avery.Id, cancellationToken);

        // Assert
        removed.Should().BeTrue();
        removedAgain.Should().BeFalse();
        await using var reader = fixture.CreateContext();
        var remaining = await reader.RecordMutes
            .Where(m => m.OwnerId == gadgetA || m.OwnerId == gadgetB)
            .Select(m => new { m.OwnerId, m.EmployeeId })
            .ToListAsync(cancellationToken);
        remaining.Should().BeEquivalentTo([
            new { OwnerId = gadgetA, EmployeeId = blake.Id },
            new { OwnerId = gadgetB, EmployeeId = avery.Id },
        ]);
    }

    [Fact]
    public async Task ReasonMutes_ShouldBeAddedOncePerReason_AndRemovedOneAtATime()
    {
        // Arrange
        var cancellationToken = TestContext.Current.CancellationToken;
        var employee = await NotificationTestData.SeedEmployee(fixture);
        await using var context = fixture.CreateContext();
        var store = new MuteStore(context);

        // Act
        var outcomes = new[]
        {
            await store.Add(ReasonMute.Create(employee.Id, NotificationReason.Following, Timestamp).Value, cancellationToken),
            await store.Add(ReasonMute.Create(employee.Id, NotificationReason.Participant, Timestamp).Value, cancellationToken),
            await store.Add(ReasonMute.Create(employee.Id, NotificationReason.Following, Timestamp).Value, cancellationToken),
        };
        var removed = await store.RemoveReasonMute(employee.Id, NotificationReason.Following, cancellationToken);

        // Assert
        outcomes.Should().Equal(true, true, false);
        removed.Should().BeTrue();
        await using var reader = fixture.CreateContext();
        var remaining = await reader.ReasonMutes
            .Where(m => m.EmployeeId == employee.Id)
            .Select(m => m.Reason)
            .ToListAsync(cancellationToken);
        remaining.Should().Equal(NotificationReason.Participant);
    }

    private static RecordMute NewRecordMute(Guid ownerId, Guid employeeId, Instant? mutedOn = null)
    {
        FollowOwners.Register(Gadget);
        return RecordMute.Create(GadgetType, ownerId, employeeId, mutedOn ?? Timestamp).Value;
    }
}
