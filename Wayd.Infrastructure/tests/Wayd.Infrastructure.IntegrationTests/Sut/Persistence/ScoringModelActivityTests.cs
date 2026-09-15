using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Scoring;
using Wayd.Common.Domain.Scoring;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Initialization;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves a scoring model's history reaches the activity log through a real save: its creation once the key is
/// assigned, the deletion of a model whose children cascade with it, and the starter model the seeder creates.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class ScoringModelActivityTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 5, 1, 8, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task SaveChanges_RecordsTheCreationWithTheAssignedKey_AndTheDeletion()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var model = ScoringModel.Create($"Atlas model {Guid.NewGuid():N}", "Deleted with its children.", EventActor.System, Now,
            scales: [("Impact", [("High", 8m), ("Low", 1m)])],
            criteria: [("Business Value", "BV", null, null, "Impact")],
            outputs: [("Score", "Score", "BV", true)]);
        context.ScoringModels.Add(model);
        await context.SaveChangesAsync(ct);

        model.Delete(EventActor.System, Now.Plus(Duration.FromHours(1))).IsSuccess.Should().BeTrue();
        context.ScoringModels.Remove(model);

        // Act
        await context.SaveChangesAsync(ct);

        // Assert
        var entries = await context.ActivityLogs.AsNoTracking()
            .Where(a => a.AggregateId == model.Id)
            .OrderBy(a => a.Timestamp).ThenBy(a => a.Ordinal)
            .ToListAsync(ct);

        entries.Select(e => e.EventType).Should().Equal(nameof(ScoringModelCreatedEvent), nameof(ScoringModelDeletedEvent));
        entries.Should().OnlyContain(e => e.AggregateType == "ScoringModel" && e.DomainArea == "Scoring");
        entries[0].Payload.Should().Contain($"\"key\":{model.Key}", "the creation event was raised once the key was assigned");
        (await context.ScoringModels.AnyAsync(m => m.Id == model.Id, ct)).Should().BeFalse();
    }

    [Fact]
    public async Task Seeder_RecordsTheCreationOfTheModelItSeeds()
    {
        // Arrange — the seeder only runs against an empty table, so every model is removed, as on a fresh install.
        var ct = TestContext.Current.CancellationToken;
        await using (var setup = _fixture.CreateContext())
        {
            await setup.Portfolios.ExecuteUpdateAsync(p => p.SetProperty(x => x.ScoringModelId, (Guid?)null), ct);
            await setup.ScoringModels.ExecuteDeleteAsync(ct);
        }

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(Now);

        // Act
        await using (var context = _fixture.CreateContext())
        {
            await new ScoringModelSeeder().Initialize(context, dateTimeProvider.Object, ct);
        }

        // Assert
        await using var verify = _fixture.CreateContext();
        var model = await verify.ScoringModels.AsNoTracking().SingleAsync(ct);

        var entry = (await verify.ActivityLogs.AsNoTracking().Where(a => a.AggregateId == model.Id).ToListAsync(ct))
            .Should().ContainSingle().Subject;
        entry.EventType.Should().Be(nameof(ScoringModelCreatedEvent));
        entry.ActorKind.Should().Be(EventActorKind.System);
        entry.Payload.Should().Contain($"\"key\":{model.Key}", "the event was raised once the key was assigned");
    }
}
