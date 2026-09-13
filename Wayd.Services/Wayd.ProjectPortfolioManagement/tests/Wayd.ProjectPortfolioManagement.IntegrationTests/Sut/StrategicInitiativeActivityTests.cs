using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProjectPortfolioManagement;
using Wayd.Common.Domain.Models.KeyPerformanceIndicators;
using Wayd.Common.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models;
using Wayd.ProjectPortfolioManagement.Domain.Models.Authorization;
using Wayd.ProjectPortfolioManagement.Domain.Models.StrategicInitiatives;
using Wayd.ProjectPortfolioManagement.IntegrationTests.Infrastructure;

namespace Wayd.ProjectPortfolioManagement.IntegrationTests.Sut;

/// <summary>
/// Proves a strategic initiative's events reach its own activity log carrying the key SQL Server assigned.
/// </summary>
/// <remarks>
/// The initiative is added through the portfolio's collection, and every event it raises before that first
/// save waits for the key in a post-persistence action on the initiative itself. Only a real save shows
/// that the action runs for an entity reached through a navigation, and in the order it was queued.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class StrategicInitiativeActivityTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task SaveChanges_RecordsAnInitiativeMovedOnBeforeItsFirstSave_AgainstTheInitiative()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await _fixture.ResetPpmData(ct);
        var now = SqlServerDbContextFixture.FixedNow;

        await using var context = _fixture.CreateContext();
        var portfolio = ProjectPortfolio.Create("Delivery", "Delivery portfolio", null, EventActor.System, now);
        portfolio.Activate(PpmActor.System, now.InUtc().Date, now);
        await context.Portfolios.AddAsync(portfolio, ct);
        await context.SaveChangesAsync(ct);

        var dateRange = new LocalDateRange(now.InUtc().Date, now.InUtc().Date.PlusDays(90));
        var initiative = portfolio.CreateStrategicInitiative("Atlas", "Move the estate", dateRange, null, EventActor.System, now).Value;
        initiative.Approve(EventActor.System, now);
        initiative.CreateKpi(
            new StrategicInitiativeKpiUpsertParameters("Uptime", null, null, 99.9, null, "%", KpiTargetDirection.Increase),
            EventActor.System, now);

        // Act
        await context.SaveChangesAsync(ct);

        // Assert
        initiative.Key.Should().BeGreaterThan(0);

        await using var verify = _fixture.CreateContext();
        var entries = await verify.ActivityLogs
            .AsNoTracking()
            .Where(a => a.AggregateId == initiative.Id)
            .OrderBy(a => a.Ordinal)
            .ToListAsync(ct);

        entries.Select(e => e.EventType).Should().Equal(
            nameof(StrategicInitiativeCreatedEvent),
            nameof(StrategicInitiativeStatusChangedEvent),
            nameof(StrategicInitiativeKpiAddedEvent));
        entries.Should().OnlyContain(e => e.AggregateType == "StrategicInitiative");
        entries.Should().OnlyContain(e => JsonDocument.Parse(e.Payload).RootElement.GetProperty("key").GetInt32() == initiative.Key);
    }
}
