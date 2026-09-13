using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.StrategicManagement;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.StrategicManagement.Domain.Models;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves a deletion event raised on the record being deleted reaches the activity log.
/// </summary>
/// <remarks>
/// EF detaches a deleted entity when the save commits, before the drain reads the change tracker, so the
/// event is only found if the context held on to the entity itself.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class DeletedAggregateActivityTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static readonly Instant CreatedAt = Instant.FromUtc(2026, 5, 1, 8, 0, 0);
    private static readonly Instant DeletedAt = Instant.FromUtc(2026, 5, 2, 9, 0, 0);

    [Fact]
    public async Task SaveChanges_RecordsTheDeletionEventOfTheRecordItDeletes()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var theme = StrategicTheme.Create(
            "Deletion seed", "Deletion seed", StrategicThemeState.Proposed, EventActor.System, CreatedAt);
        context.StrategicThemes.Add(theme);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        theme.Delete(EventActor.System, DeletedAt).IsSuccess.Should().BeTrue();
        context.StrategicThemes.Remove(theme);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var entries = await context.ActivityLogs
            .AsNoTracking()
            .Where(a => a.AggregateId == theme.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        entries.Should().ContainSingle(a => a.EventType == nameof(StrategicThemeDeletedEvent))
            .Which.Category.Should().Be(ActivityCategory.Removed);
    }
}
