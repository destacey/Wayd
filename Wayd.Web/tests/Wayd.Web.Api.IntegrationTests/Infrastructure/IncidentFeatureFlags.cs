using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.FeatureManagement.Commands;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.FeatureManagement;

namespace Wayd.Web.Api.IntegrationTests.Infrastructure;

/// <summary>
/// Turns the product management and incident management flags on or off, which every incident command and
/// query needs on. They are seeded off, and a test that turns them on turns them off again when it ends.
/// </summary>
public static class IncidentFeatureFlags
{
    public static async Task Set(WaydSqlServerApiFactory factory, bool enabled)
    {
        using var scope = factory.Services.CreateScope();
        await scope.ActAsHarnessAdministrator();
        var ids = await scope.ServiceProvider.GetRequiredService<IFeatureManagementDbContext>().FeatureFlags
            .Where(f => f.Name == FeatureFlags.Names.ProductManagement || f.Name == FeatureFlags.Names.IncidentManagement)
            .Select(f => f.Id)
            .ToListAsync(TestContext.Current.CancellationToken);

        var dispatcher = scope.ServiceProvider.GetRequiredService<IDispatcher>();
        foreach (var id in ids)
        {
            var toggled = await dispatcher.Send(new ToggleFeatureFlagCommand(id, enabled), TestContext.Current.CancellationToken);
            Assert.True(toggled.IsSuccess, toggled.IsFailure ? toggled.Error : null);
        }
    }
}
