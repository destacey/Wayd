using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.AppIntegration.Application.Connections.ExternalSystems;
using Wayd.AppIntegration.Domain.Models;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events;
using Wayd.Infrastructure.DataProtection;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Infrastructure.Persistence.Initialization;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves the startup backfill ties existing connections to their outside systems against a real database, where a
/// system added in the same save is not yet found by a query and the unique index on connector and system id holds.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class ExternalSystemBackfillTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 6, 1, 9, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = InstallSecretProtector(fixture);

    /// <summary>
    /// Connections encrypt their configuration on save, through a protector the host installs at startup. Installed
    /// here only when nothing has, so a protector another test set is never swapped from under it.
    /// </summary>
    private static SqlServerDbContextFixture InstallSecretProtector(SqlServerDbContextFixture fixture)
    {
        try
        {
            _ = SecretProtectorAccessor.Current;
        }
        catch (InvalidOperationException)
        {
            SecretProtectorAccessor.Set(new AesGcmSecretProtector(RandomNumberGenerator.GetBytes(32)));
        }

        return fixture;
    }

    [Fact]
    public async Task Run_GivesTwoConnectionsToOneTenantOneSharedSystem()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var tenantId = $"tenant-{Guid.NewGuid():N}";
        var first = await SeedEntra(tenantId, ct);
        var second = await SeedEntra(tenantId, ct);

        // Act
        await RunBackfill(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        var system = await verify.ExternalSystems.SingleAsync(s => s.SystemId == tenantId, ct);
        system.Connector.Should().Be(Connector.Entra);
        system.Name.Should().Be(tenantId);

        var linked = await verify.Connections
            .Where(c => c.Id == first.Id || c.Id == second.Id)
            .Select(c => c.ExternalSystemId)
            .ToListAsync(ct);
        linked.Should().HaveCount(2).And.OnlyContain(id => id == system.Id);
    }

    [Fact]
    public async Task Run_LinksADeletedConnection()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var tenantId = $"tenant-{Guid.NewGuid():N}";
        var connection = await SeedEntra(tenantId, ct);
        await using (var context = _fixture.CreateContext())
        {
            context.Connections.Remove(await context.Connections.SingleAsync(c => c.Id == connection.Id, ct));
            await context.SaveChangesAsync(ct);
        }

        // Act
        await RunBackfill(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        var systemId = await verify.ExternalSystems.Where(s => s.SystemId == tenantId).Select(s => s.Id).SingleAsync(ct);
        var linked = await verify.Connections.IgnoreQueryFilters()
            .Where(c => c.Id == connection.Id)
            .Select(c => c.ExternalSystemId)
            .SingleAsync(ct);
        linked.Should().Be(systemId);
    }

    [Fact]
    public async Task Run_LeavesAnAzureDevOpsConnectionWithNoSystemIdUnlinked()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var connection = AzureDevOpsBoardsConnection.Create(
            "Boards", null, null, new AzureDevOpsBoardsConnectionConfiguration($"org-{Guid.NewGuid():N}", "pat"), false, null, EventActor.System, Now);
        await using (var context = _fixture.CreateContext())
        {
            context.Connections.Add(connection);
            await context.SaveChangesAsync(ct);
        }

        // Act
        await RunBackfill(ct);

        // Assert
        await using var verify = _fixture.CreateContext();
        var linked = await verify.Connections.Where(c => c.Id == connection.Id).Select(c => c.ExternalSystemId).SingleAsync(ct);
        linked.Should().BeNull();
    }

    private async Task<EntraConnection> SeedEntra(string tenantId, CancellationToken ct)
    {
        var connection = EntraConnection.Create("People", null, new EntraConnectionConfiguration(tenantId, "client-1", "secret"), true, EventActor.System, Now);
        await using var context = _fixture.CreateContext();
        context.Connections.Add(connection);
        await context.SaveChangesAsync(ct);
        return connection;
    }

    private async Task RunBackfill(CancellationToken ct)
    {
        await using var context = _fixture.CreateContext();
        var clock = new Mock<IDateTimeProvider>();
        clock.SetupGet(c => c.Now).Returns(Now);

        var linker = new ExternalSystemLinker(context, Mock.Of<IAzureDevOpsUrlResolver>());
        var backfill = new ExternalSystemBackfill(context, linker, clock.Object, NullLogger<ExternalSystemBackfill>.Instance);

        await backfill.Run(ct);
    }
}
