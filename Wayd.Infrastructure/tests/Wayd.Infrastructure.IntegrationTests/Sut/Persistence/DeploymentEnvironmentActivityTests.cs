using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.ProductManagement;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Proves an environment's edits and its return to use reach its activity through a real save.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class DeploymentEnvironmentActivityTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 5, 1, 8, 0, 0);

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task SaveChanges_RecordsTheRenameAndTheReinstatement()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        await using var context = _fixture.CreateContext();
        var name = $"env-{Guid.NewGuid():N}"[..20];
        var environment = DeploymentEnvironment.Create(name, EnvironmentCategory.Staging, 2, EventActor.System, Now);
        context.DeploymentEnvironments.Add(environment);
        await context.SaveChangesAsync(ct);

        environment.Deactivate(EventActor.System, Now.Plus(Duration.FromMinutes(1))).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(ct);

        // Act
        environment.Activate(EventActor.System, Now.Plus(Duration.FromMinutes(2))).IsSuccess.Should().BeTrue();
        environment.Update($"{name}-eu", 5, EventActor.System, Now.Plus(Duration.FromMinutes(3))).IsSuccess.Should().BeTrue();
        await context.SaveChangesAsync(ct);

        // Assert
        var entries = await context.ActivityLogs.AsNoTracking()
            .Where(a => a.AggregateId == environment.Id)
            .OrderBy(a => a.Timestamp).ThenBy(a => a.Ordinal)
            .ToListAsync(ct);

        entries.Select(e => e.EventType).Should().Equal(
            nameof(EnvironmentAddedEvent),
            nameof(EnvironmentRetiredEventV2),
            nameof(EnvironmentReinstatedEvent),
            nameof(EnvironmentDetailsUpdatedEvent));
        entries.Should().OnlyContain(e => e.AggregateType == "DeploymentEnvironment" && e.DomainArea == "ProductManagement");
        entries[2].Category.Should().Be(ActivityCategory.StateChanged);
        entries[3].Category.Should().Be(ActivityCategory.Updated);
        entries[3].Payload.Should().Contain($"\"name\":\"{name}-eu\"").And.Contain($"\"previous\":{{\"name\":\"{name}\",\"ringOrder\":2}}");
    }
}
