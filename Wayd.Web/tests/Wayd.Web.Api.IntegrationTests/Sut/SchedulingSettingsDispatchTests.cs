using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.SystemSettings.Scheduling.Commands;
using Wayd.Common.Application.SystemSettings.Scheduling.Queries;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// The scheduling settings through the real pipeline: the generated handlers, the store behind service
/// location, the row and its activity entry in SQL Server, and the cache a save must clear.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class SchedulingSettingsDispatchTests(WaydSqlServerApiFactory factory)
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    // One sequence, because the section is a single row the whole collection shares.
    [Fact]
    public async Task SchedulingSettings_ReadDefaults_ThenRecordEachChange_AndRejectAnUnknownTimeZone()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        // Act — nothing saved yet
        var defaults = await dispatcher.Send(new GetSchedulingSettingsQuery(), ct);

        // Assert
        Assert.Equal("UTC", defaults.DefaultTimeZone);
        Assert.Equal(1, defaults.DefaultCommitmentGraceDays);

        // Act — a change
        var saved = await dispatcher.Send(new UpdateSchedulingSettingsCommand("America/Chicago", 2), ct);
        var afterSave = await dispatcher.Send(new GetSchedulingSettingsQuery(), ct);
        var activities = await dispatcher.Send(new GetSchedulingSettingsActivitiesQuery(), ct);

        // Assert
        Assert.True(saved.IsSuccess, saved.IsFailure ? saved.Error : null);
        Assert.Equal("America/Chicago", afterSave.DefaultTimeZone);
        Assert.Equal(2, afterSave.DefaultCommitmentGraceDays);

        var entry = Assert.Single(activities.Items);
        Assert.Equal("SystemSettingsSectionValuesChangedEvent", entry.EventType);
        using var payload = JsonDocument.Parse(entry.Payload);
        Assert.Equal("UTC", payload.RootElement.GetProperty("previous").GetProperty("defaultTimeZone").GetString());
        Assert.Equal(1, payload.RootElement.GetProperty("previous").GetProperty("defaultCommitmentGraceDays").GetInt32());
        Assert.Equal("America/Chicago", payload.RootElement.GetProperty("current").GetProperty("defaultTimeZone").GetString());
        Assert.Equal(2, payload.RootElement.GetProperty("current").GetProperty("defaultCommitmentGraceDays").GetInt32());

        // Act — saving the same values, then an id the tz database does not know
        var unchanged = await dispatcher.Send(new UpdateSchedulingSettingsCommand("America/Chicago", 2), ct);
        var invalid = await dispatcher.Send(new UpdateSchedulingSettingsCommand("Mars/Olympus_Mons", 2), ct);
        var afterInvalid = await dispatcher.Send(new GetSchedulingSettingsQuery(), ct);
        var finalActivities = await dispatcher.Send(new GetSchedulingSettingsActivitiesQuery(), ct);

        // Assert
        Assert.True(unchanged.IsSuccess);
        Assert.True(invalid.IsFailure);
        Assert.Equal("America/Chicago", afterInvalid.DefaultTimeZone);
        Assert.Single(finalActivities.Items);
    }
}
