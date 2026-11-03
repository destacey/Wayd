using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events.Planning.EstimationScales;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Initialization;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the starter scales are seeded the way an install needs them before the host starts: saved without
/// publishing, each creation recorded in the activity log, and Demand Management given its copy of each.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class EstimationScaleSeederTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 5, 1, 8, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Initialize_SeedsTheScalesWithTheirActivityAndTheirDemandCopies()
    {
        // Arrange — the seeder only runs against an empty table, so every scale is removed, as on a fresh install.
        // Other tests in the collection leave sessions, desks and estimates pointing at scales, so those go first.
        var ct = TestContext.Current.CancellationToken;
        await using (var setup = _fixture.CreateContext())
        {
            await setup.Database.ExecuteSqlRawAsync(
                """
                UPDATE [DemandManagement].[IntakeRequests] SET [EstimateValue] = NULL, [EstimateScaleVersionId] = NULL,
                    [EstimatePosition] = NULL, [EstimateScaleName] = NULL, [EstimateScaleVersionNumber] = NULL;
                UPDATE [DemandManagement].[IntakeDesks] SET [EstimationScaleVersionId] = NULL;
                """, ct);
            await setup.DemandEstimationScales.ExecuteDeleteAsync(ct);
            await setup.PokerSessions.ExecuteDeleteAsync(ct);
            await setup.EstimationScales.ExecuteDeleteAsync(ct);
        }

        var dateTimeProvider = new Mock<IDateTimeProvider>();
        dateTimeProvider.SetupGet(d => d.Now).Returns(Now);

        // Act
        await using (var context = _fixture.CreateContext())
        {
            await new EstimationScaleSeeder().Initialize(context, dateTimeProvider.Object, ct);
        }

        // Assert
        await using var verify = _fixture.CreateContext();
        var scales = await verify.EstimationScales.AsNoTracking().Include(s => s.Versions).ToListAsync(ct);
        scales.Select(s => s.Name).Should().BeEquivalentTo(["Fibonacci", "Modified Fibonacci", "T-Shirt Sizes", "Simple"]);

        var copies = await verify.DemandEstimationScales.AsNoTracking().Include(s => s.Versions).ToListAsync(ct);
        copies.Select(c => (c.Id, c.Key, c.Name, c.IsActive, c.CurrentVersion.Id))
            .Should().BeEquivalentTo(scales.Select(s => (s.Id, s.Key, s.Name, s.IsActive, s.Versions.Single().Id)));

        var scaleIds = scales.Select(s => s.Id).ToList();
        var entries = await verify.ActivityLogs.AsNoTracking().Where(a => scaleIds.Contains(a.AggregateId)).ToListAsync(ct);
        entries.Should().HaveCount(scales.Count).And.OnlyContain(e => e.EventType == nameof(EstimationScaleCreatedEvent));
        foreach (var scale in scales)
            entries.Single(e => e.AggregateId == scale.Id).Payload.Should().Contain($"\"key\":{scale.Key}");
    }
}
