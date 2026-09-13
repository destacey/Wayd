using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Wayd.Infrastructure;
using Wayd.Web.Api.IntegrationTests.Infrastructure;

namespace Wayd.Web.Api.IntegrationTests.Sut;

/// <summary>
/// Pins what the two health endpoints mean: readiness includes the database, liveness does not.
/// </summary>
/// <remarks>
/// Aspire waits on the readiness endpoint, and an orchestrator restarts a process whose liveness check fails.
/// A database check that leaked into liveness would restart the API into the same outage.
/// </remarks>
[Collection(SqlServerApiTestCollection.Name)]
public sealed class HealthEndpointTests(WaydSqlServerApiFactory factory)
{
    private const string DatabaseCheck = "database";

    private readonly WaydSqlServerApiFactory _factory = factory;

    [Fact]
    public async Task Health_ReportsHealthy_WhenTheDatabaseIsReachable()
    {
        // Arrange
        var client = _factory.CreateClient();

        // Act
        var response = await client.GetAsync(ServiceEndpoints.HealthEndpointPath, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task HealthChecks_IncludeTheDatabaseInReadinessButNotLiveness()
    {
        // Arrange
        var healthChecks = _factory.Services.GetRequiredService<HealthCheckService>();

        // Act
        var readiness = await healthChecks.CheckHealthAsync(TestContext.Current.CancellationToken);
        var liveness = await healthChecks.CheckHealthAsync(r => r.Tags.Contains("live"), TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HealthStatus.Healthy, readiness.Entries[DatabaseCheck].Status);
        Assert.DoesNotContain(DatabaseCheck, liveness.Entries.Keys);
    }
}
