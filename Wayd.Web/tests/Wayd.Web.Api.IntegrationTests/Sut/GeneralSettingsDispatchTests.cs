using Wayd.Common.Application.Models;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.SystemSettings.General.Commands;
using Wayd.Common.Application.SystemSettings.General.Queries;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// The general settings through the real pipeline: the generated handlers, the store behind service location,
/// the row and its activity entry in SQL Server, and the cache a save must clear.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class GeneralSettingsDispatchTests(WaydSqlServerApiFactory factory)
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    // One sequence, because the section is a single row the whole collection shares.
    [Fact]
    public async Task GeneralSettings_ReadDefaults_ThenRecordEachChange_AndRejectAnUnknownTimeZone()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        using var scope = _factory.Services.CreateScope();
        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();

        // Act — nothing saved yet
        var defaults = await dispatcher.Send(new GetGeneralSettingsQuery(), ct);

        // Assert
        Assert.Equal("UTC", defaults.TimeZone);

        // Act — a change
        var saved = await dispatcher.Send(new UpdateGeneralSettingsCommand("America/Chicago"), ct);
        var afterSave = await dispatcher.Send(new GetGeneralSettingsQuery(), ct);
        var activities = await dispatcher.Send(new GetGeneralSettingsActivitiesQuery(new PageRequest()), ct);

        // Assert
        Assert.True(saved.IsSuccess, saved.IsFailure ? saved.Error : null);
        Assert.Equal("America/Chicago", afterSave.TimeZone);

        var entry = Assert.Single(activities.Items);
        Assert.Equal("SystemSettingsSectionValuesChangedEvent", entry.EventType);
        using var payload = JsonDocument.Parse(entry.Payload);
        Assert.Equal("UTC", payload.RootElement.GetProperty("previous").GetProperty("timeZone").GetString());
        Assert.Equal("America/Chicago", payload.RootElement.GetProperty("current").GetProperty("timeZone").GetString());

        // Act — saving the same value, then an id the tz database does not know
        var unchanged = await dispatcher.Send(new UpdateGeneralSettingsCommand("America/Chicago"), ct);
        var invalid = await dispatcher.Send(new UpdateGeneralSettingsCommand("Mars/Olympus_Mons"), ct);
        var afterInvalid = await dispatcher.Send(new GetGeneralSettingsQuery(), ct);
        var finalActivities = await dispatcher.Send(new GetGeneralSettingsActivitiesQuery(new PageRequest()), ct);

        // Assert
        Assert.True(unchanged.IsSuccess);
        Assert.True(invalid.IsFailure);
        Assert.Equal("America/Chicago", afterInvalid.TimeZone);
        Assert.Single(finalActivities.Items);
    }
}
