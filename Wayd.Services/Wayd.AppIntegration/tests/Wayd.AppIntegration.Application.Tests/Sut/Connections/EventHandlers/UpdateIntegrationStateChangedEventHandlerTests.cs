using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.EventHandlers;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.EventHandlers;

public class UpdateIntegrationStateChangedEventHandlerTests
{
    private static readonly Instant _now = Instant.FromUtc(2026, 10, 31, 12, 0, 0);

    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly UpdateIntegrationStateChangedEventHandler _sut;

    public UpdateIntegrationStateChangedEventHandlerTests()
    {
        _sut = new UpdateIntegrationStateChangedEventHandler(_db, NullLogger<UpdateIntegrationStateChangedEventHandler>.Instance);
    }

    [Fact]
    public async Task Handle_UpdatesEveryConnectionThatSharesTheWorkProcess()
    {
        // Arrange — Azure DevOps processes share one id across organizations, so two connections
        // can link the same Wayd work process.
        var processExternalId = Guid.CreateVersion7();
        var processInternalId = Guid.CreateVersion7();
        var first = ConnectionWithProcess("org-one", processExternalId, processInternalId);
        var second = ConnectionWithProcess("org-two", processExternalId, processInternalId);
        _db.AddAzureDevOpsBoardsConnections([first, second]);

        var @event = new IntegrationStateChangedEvent<Guid>(
            SystemContext.WorkWorkProcess,
            IntegrationState<Guid>.Create(processInternalId, false),
            EventActor.System,
            _now);

        // Act
        await _sut.Handle(@event, TestContext.Current.CancellationToken);

        // Assert
        first.Configuration.WorkProcesses.Single().IntegrationIsActive.Should().BeFalse();
        second.Configuration.WorkProcesses.Single().IntegrationIsActive.Should().BeFalse();
        _db.SaveChangesCallCount.Should().Be(1);
    }

    private static AzureDevOpsBoardsConnection ConnectionWithProcess(string organization, Guid processExternalId, Guid processInternalId)
    {
        var process = AzureDevOpsBoardsWorkProcess.Create(processExternalId, "Agile", null);
        process.AddIntegrationState(IntegrationState<Guid>.Create(processInternalId, true));

        var config = new AzureDevOpsBoardsConnectionConfiguration(organization, "pat", processes: [process]);
        return AzureDevOpsBoardsConnection.Create(organization, null, $"system-{organization}", config, true, null, EventActor.System, _now);
    }
}
