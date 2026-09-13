using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.StrategicManagement;
using Wayd.ProjectPortfolioManagement.Application;
using Wayd.StrategicManagement.Application.StrategicThemes.Commands;
using Wayd.Web.Api.IntegrationTests.Infrastructure;
using Wolverine;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// A StrategicManagement theme is copied into PPM by the durable <c>StrategicTheme*</c> events. These run through
/// the real host, so a missing route or handler for an event leaves the copy stale rather than failing a build.
/// </summary>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class StrategicThemeReplicationTests(WaydSqlServerApiFactory factory)
{
    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task UpdateStrategicTheme_ReplicatesTheNewDetailsToPpm()
    {
        // Arrange
        _ = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var themeId = await CreateReplicatedTheme(ct);
        var name = $"Renamed Theme {Guid.NewGuid():N}"[..24];

        // Act
        using (var dispatchScope = _factory.Services.CreateScope())
        {
            var result = await dispatchScope.ServiceProvider.GetRequiredService<IDispatcher>().Send(
                new UpdateStrategicThemeCommand(themeId, name, "Redescribed"), ct);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
        }

        // Assert
        Assert.True(await WaitFor(
            sp => sp.GetRequiredService<IProjectPortfolioManagementDbContext>().PpmStrategicThemes
                .AnyAsync(t => t.Id == themeId && t.Name == name && t.Description == "Redescribed", ct),
            ct), "the PPM copy should take the new name and description");
    }

    [Fact]
    public async Task SupersededStrategicThemeUpdatedEvent_StillInTheOutbox_IsAppliedToTheCopy()
    {
        // Arrange — an envelope written as the superseded type before the switch, delivered through the real
        // durable route.
        _ = _factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;
        var themeId = await CreateReplicatedTheme(ct);
        var name = $"Legacy Theme {Guid.NewGuid():N}"[..24];

        // Act
        using (var publishScope = _factory.Services.CreateScope())
        {
            var now = publishScope.ServiceProvider.GetRequiredService<IDateTimeProvider>().Now;
#pragma warning disable CS0618 // the retired type is exactly what is under test
            var legacy = new StrategicThemeUpdatedEvent(themeId, name, "Legacy description", StrategicThemeState.Proposed,
                EventActor.System, now.Plus(Duration.FromMinutes(1)));
#pragma warning restore CS0618

            await publishScope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(legacy);
        }

        // Assert
        Assert.True(await WaitFor(
            sp => sp.GetRequiredService<IProjectPortfolioManagementDbContext>().PpmStrategicThemes
                .AnyAsync(t => t.Id == themeId && t.Name == name, ct),
            ct), "the PPM copy should apply the superseded event");
    }

    private async Task<Guid> CreateReplicatedTheme(CancellationToken ct)
    {
        Guid themeId;
        using (var dispatchScope = _factory.Services.CreateScope())
        {
            // Theme names are unique, and every test in the collection shares the database.
            var result = await dispatchScope.ServiceProvider.GetRequiredService<IDispatcher>().Send(
                new CreateStrategicThemeCommand($"Theme {Guid.NewGuid():N}"[..24], "Replication test theme"), ct);
            Assert.True(result.IsSuccess, result.IsFailure ? result.Error : null);
            themeId = result.Value.Id;
        }

        Assert.True(await WaitFor(
            sp => sp.GetRequiredService<IProjectPortfolioManagementDbContext>().PpmStrategicThemes.AnyAsync(t => t.Id == themeId, ct),
            ct), "the PPM copy should exist before the change under test is made");

        return themeId;
    }

    private async Task<bool> WaitFor(Func<IServiceProvider, Task<bool>> condition, CancellationToken ct)
    {
        var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            using var scope = _factory.Services.CreateScope();
            if (await condition(scope.ServiceProvider))
            {
                return true;
            }

            await Task.Delay(250, ct);
        }

        return false;
    }
}
