using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Models;
using Wayd.DemandManagement.Application.IntakeChannels.Queries;
using Wayd.DemandManagement.Application.IntakeDesks.Queries;
using Wayd.DemandManagement.Domain.Models;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Initialization;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Runs the channel reads that depend on a desk's chosen channels against SQL Server, where an in-memory fake
/// cannot show they translate, and the seeder that starts the company list.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class IntakeChannelQueryTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 27, 9, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task GetIntakeChannels_ForADeskOfferingEveryChannel_ReturnsEveryActiveChannel()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (desk, chosen, archived) = await SeedDeskAndChannels(chooseChannels: false, ct);

        // Act
        await using var context = _fixture.CreateContext();
        var result = await new GetIntakeChannelsQueryHandler(context)
            .Handle(new GetIntakeChannelsQuery(DeskId: desk.Id), ct);

        // Assert
        result.Select(c => c.Id).Should().Contain(chosen.Id).And.NotContain(archived.Id);
    }

    [Fact]
    public async Task GetIntakeChannels_ForADeskThatChoseChannels_ReturnsOnlyItsActiveChosenOnes()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (desk, chosen, _) = await SeedDeskAndChannels(chooseChannels: true, ct);

        // Act
        await using var context = _fixture.CreateContext();
        var result = await new GetIntakeChannelsQueryHandler(context)
            .Handle(new GetIntakeChannelsQuery(DeskId: desk.Id), ct);

        // Assert
        result.Select(c => c.Id).Should().Equal(chosen.Id);
    }

    [Fact]
    public async Task GetIntakeDesk_ForADeskThatChoseChannels_ListsThemArchivedOnesIncluded()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var (desk, chosen, archived) = await SeedDeskAndChannels(chooseChannels: true, ct);

        // Act
        await using var context = _fixture.CreateContext();
        var result = await new GetIntakeDeskQueryHandler(context, Principal())
            .Handle(new GetIntakeDeskQuery(new IdOrKey(desk.Id)), ct);

        // Assert
        result.Should().NotBeNull();
        result!.OffersAllChannels.Should().BeFalse();
        result.ChannelIds.Should().BeEquivalentTo([chosen.Id, archived.Id]);
    }

    [Fact]
    public async Task Seeder_OnAnEmptyList_RecordsTheCreationOfEveryChannelItSeeds()
    {
        // Arrange — the seeder runs only against an empty list, so the list is emptied inside a unit of work that
        // is never committed, leaving the shared database as it was.
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        await using var unitOfWork = await context.BeginUnitOfWork(ct);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [DemandManagement].[IntakeDeskChannels];", ct);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [DemandManagement].[IntakeRequests];", ct);
        await context.Database.ExecuteSqlRawAsync("DELETE FROM [DemandManagement].[IntakeChannels];", ct);
        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(Now);

        // Act
        await new IntakeChannelSeeder().Initialize(context, dateTimeProvider.Object, ct);

        // Assert
        var channelIds = await context.IntakeChannels.AsNoTracking().Select(c => c.Id).ToListAsync(ct);
        channelIds.Should().HaveCount(8);
        var entries = await context.ActivityLogs.AsNoTracking()
            .Where(a => channelIds.Contains(a.AggregateId))
            .ToListAsync(ct);
        entries.Should().HaveCount(8).And.OnlyContain(a =>
            a.EventType == nameof(IntakeChannelCreatedEvent) && a.ActorKind == EventActorKind.System);
        entries.Select(a => a.AggregateId).Should().BeEquivalentTo(channelIds);
    }

    /// <summary>
    /// A desk with an active and an archived channel, both chosen when <paramref name="chooseChannels"/> is set.
    /// </summary>
    private async Task<(IntakeDesk Desk, IntakeChannel Active, IntakeChannel Archived)> SeedDeskAndChannels(
        bool chooseChannels, CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        var desk = IntakeDesk.Create($"Desk-{Guid.NewGuid():N}"[..30], "Anything", IntakeDeskVisibility.Open,
            true, true, true, [], EventActor.System, Now);
        var active = IntakeChannel.Create($"Channel-{Guid.NewGuid():N}"[..30], null, 1, EventActor.System, Now);
        var archived = IntakeChannel.Create($"Channel-{Guid.NewGuid():N}"[..30], null, 2, EventActor.System, Now);
        archived.Archive(EventActor.System, Now);
        if (chooseChannels)
            desk.ChangeChannels(false, [active.Id, archived.Id], EventActor.System, Now);

        context.IntakeChannels.AddRange(active, archived);
        context.IntakeDesks.Add(desk);
        await context.SaveChangesAsync(ct);

        return (desk, active, archived);
    }

    private static ICurrentPrincipal Principal()
    {
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(Guid.NewGuid());
        return principal.Object;
    }
}
