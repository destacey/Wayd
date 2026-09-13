using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Wayd.Common.Domain.Activities;
using Wayd.Common.Domain.Events;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Runs the Refile-StrategicInitiative-Activity migration over entries written before initiatives had an
/// activity log of their own.
/// </summary>
/// <remarks>
/// The migration reads the initiative's id out of the stored JSON, so only real SQL Server against a real
/// payload shows that the path matches what the serializer wrote.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class RefileStrategicInitiativeActivityMigrationTests(SqlServerDbContextFixture fixture)
{
    private const string MigrationBefore = "20260913165252_Add-ActivityLog-Category";

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Up_MovesAnInitiativesCreationAndDeletionOntoTheInitiative_AndLeavesThePayload()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var portfolioId = Guid.CreateVersion7();
        var initiativeId = Guid.CreateVersion7();
        var payload = $$"""{"portfolioId":"{{portfolioId}}","strategicInitiativeId":"{{initiativeId}}","name":"Atlas","eventVersion":"1.0"}""";
        var otherPortfolioEntryId = Guid.CreateVersion7();

        await using (var context = _fixture.CreateContext())
        {
            await context.GetService<IMigrator>().MigrateAsync(MigrationBefore, ct);

            context.ActivityLogs.AddRange(
                Entry("StrategicInitiativeCreatedEvent", ActivityCategory.Created, portfolioId, payload, "Strategic Initiative Created on Project Portfolio"),
                Entry("StrategicInitiativeDeletedEvent", ActivityCategory.Removed, portfolioId, payload, "Strategic Initiative Deleted on Project Portfolio"),
                Entry("ProjectPortfolioCreatedEvent", ActivityCategory.Created, portfolioId, $$"""{"id":"{{portfolioId}}"}""", "Project Portfolio Created", otherPortfolioEntryId));
            await context.SaveChangesAsync(ct);

            // Act
            await context.Database.MigrateAsync(ct);
        }

        // Assert
        await using var verify = _fixture.CreateContext();
        var initiativeEntries = await verify.ActivityLogs.AsNoTracking()
            .Where(a => a.AggregateId == initiativeId)
            .ToListAsync(ct);

        initiativeEntries.Should().HaveCount(2);
        initiativeEntries.Should().OnlyContain(a => a.AggregateType == "StrategicInitiative");
        initiativeEntries.Select(a => a.Summary).Should().BeEquivalentTo("Strategic Initiative Created", "Strategic Initiative Deleted");
        initiativeEntries.Single(a => a.EventType == "StrategicInitiativeCreatedEvent").Payload.Should().Be(payload);

        var portfolioEntries = await verify.ActivityLogs.AsNoTracking()
            .Where(a => a.AggregateId == portfolioId)
            .ToListAsync(ct);
        portfolioEntries.Should().ContainSingle().Which.Id.Should().Be(otherPortfolioEntryId);
    }

    private static ActivityLogEntry Entry(string eventType, ActivityCategory category, Guid portfolioId, string payload, string summary, Guid? id = null) =>
        new(id ?? Guid.CreateVersion7(), eventType, category, "Ppm", "ProjectPortfolio", portfolioId,
            EventActor.System, Instant.FromUtc(2026, 3, 1, 12, 0, 0), ordinal: 0, correlationId: null, payload, summary);
}
