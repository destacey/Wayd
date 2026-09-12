using System.Text.Json;
using Mapster;
using Mapster.Utils;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Events;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.StrategicManagement.Domain.Models;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the activity log totally orders itself when several events share a timestamp.
/// </summary>
/// <remarks>
/// Only a real database can show this. The events of one command tie on the column reads sort by wherever the
/// caller reads the clock once for a whole batch — the import definitions and the sync handlers do — and a tie
/// is exactly where SQL Server is free to return rows in a different order each time it is asked. These pass
/// one <c>Instant</c> to each domain call to reproduce that deterministically. The in-memory provider returns
/// insertion order for tied rows, so it reports success whether or not the tiebreak exists.
/// </remarks>
[Collection(nameof(SqlServerTestCollection))]
public sealed class ActivityLogOrderingTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    private static readonly Instant CreatedAt = Instant.FromUtc(2026, 4, 1, 8, 0, 0);
    private static readonly Instant EditedAt = Instant.FromUtc(2026, 4, 2, 9, 0, 0);

    /// <summary>
    /// Registers the Mapster mappings the reader projects through, as <c>ConfigureServices</c> does at
    /// startup. Without it a projection falls back to convention and silently drops configured members.
    /// </summary>
    private static readonly Lazy<bool> Mappings = new(() =>
    {
        var assembly = typeof(ActivityLogDto).Assembly;
        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);
        return true;
    });

    [Fact]
    public async Task SaveChanges_NumbersEventsSharingOneTimestamp_InTheOrderTheyWereRaised()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var theme = await SeedTheme(context);

        theme.Update("First edit", "First", EventActor.System, EditedAt);
        theme.Update("Second edit", "Second", EventActor.System, EditedAt);
        theme.Update("Third edit", "Third", EventActor.System, EditedAt);

        // Act
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        // Assert
        var entries = await context.ActivityLogs
            .AsNoTracking()
            .Where(a => a.AggregateId == theme.Id && a.Timestamp == EditedAt)
            .OrderBy(a => a.Ordinal)
            .ToListAsync(TestContext.Current.CancellationToken);

        entries.Should().HaveCount(3);
        entries.Select(e => e.Ordinal).Should().BeInAscendingOrder().And.OnlyHaveUniqueItems();
        entries.Select(e => NameIn(e.Payload)).Should().Equal("First edit", "Second edit", "Third edit");
    }

    [Fact]
    public async Task Read_ReturnsEntriesSharingOneTimestamp_NewestRaisedFirst()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var theme = await SeedTheme(context);

        theme.Update("First edit", "First", EventActor.System, EditedAt);
        theme.Update("Second edit", "Second", EventActor.System, EditedAt);
        theme.Update("Third edit", "Third", EventActor.System, EditedAt);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _ = Mappings.Value;
        var reader = new ActivityLogReader(context);

        // Act
        var page = await reader.Read(theme.Id, cancellationToken: TestContext.Current.CancellationToken);

        // Assert
        // The creation entry stays oldest: it was written first but also carries an earlier timestamp, which
        // is why Ordinal is the tiebreak rather than the sort key. Ordering on Ordinal alone would put a
        // replayed historical fact at the top of the feed.
        page.Items.Select(i => NameIn(i.Payload))
            .Should().Equal("Third edit", "Second edit", "First edit", "Ordering seed");
    }

    [Fact]
    public async Task Read_PagesThroughEntriesSharingOneTimestamp_WithoutRepeatingOrSkippingAny()
    {
        // Arrange
        await using var context = _fixture.CreateContext();
        var theme = await SeedTheme(context);

        theme.Update("First edit", "First", EventActor.System, EditedAt);
        theme.Update("Second edit", "Second", EventActor.System, EditedAt);
        theme.Update("Third edit", "Third", EventActor.System, EditedAt);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        _ = Mappings.Value;
        var reader = new ActivityLogReader(context);

        // Act
        var paged = new List<ActivityLogDto>();
        for (var page = 1; page <= 4; page++)
        {
            var result = await reader.Read(
                theme.Id, page: page, pageSize: 1, cancellationToken: TestContext.Current.CancellationToken);

            result.Items.Should().ContainSingle();
            paged.Add(result.Items[0]);
        }

        // Assert
        // Against the order the events were raised in rather than against a second read of the same query:
        // a page that repeated or skipped an entry would agree with any other read that made the same
        // arbitrary choice, so self-consistency is not the property under test.
        paged.Select(i => i.Id).Should().OnlyHaveUniqueItems();
        paged.Select(i => NameIn(i.Payload))
            .Should().Equal("Third edit", "Second edit", "First edit", "Ordering seed");
    }

    /// <summary>Creates and saves a theme, so its creation entry is already in the log.</summary>
    private static async Task<StrategicTheme> SeedTheme(WaydDbContext context)
    {
        var theme = StrategicTheme.Create(
            "Ordering seed", "Ordering seed", StrategicThemeState.Proposed, EventActor.System, CreatedAt);

        context.StrategicThemes.Add(theme);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);

        return theme;
    }

    private static string? NameIn(string payload) =>
        JsonDocument.Parse(payload).RootElement.GetProperty("name").GetString();
}
