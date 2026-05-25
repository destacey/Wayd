using CSharpFunctionalExtensions;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Commands.AzureDevOps;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Domain.Models;
using Wayd.Tests.Shared;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Commands.AzureDevOps;

public class SyncAzureDevOpsConnectionConfigurationCommandHandlerTests
{
    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly TestingDateTimeProvider _clock = new(new DateTime(2026, 5, 1, 12, 0, 0));
    private readonly Mock<IAzureDevOpsService> _azureDevOpsService = new();
    private readonly Mock<ISender> _sender = new();
    private readonly SyncAzureDevOpsConnectionConfigurationCommandHandler _sut;

    public SyncAzureDevOpsConnectionConfigurationCommandHandlerTests()
    {
        _sut = new SyncAzureDevOpsConnectionConfigurationCommandHandler(
            _db,
            _clock,
            Mock.Of<ILogger<SyncAzureDevOpsConnectionConfigurationCommandHandler>>(),
            _azureDevOpsService.Object,
            _sender.Object);
    }

    private AzureDevOpsBoardsConnection CreateConnectionWithProcess(Guid externalId, Guid internalId, bool integrationIsActive = true)
    {
        var process = AzureDevOpsBoardsWorkProcess.Create(externalId, "Agile", "test");
        process.AddIntegrationState(IntegrationState<Guid>.Create(internalId, integrationIsActive));

        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT", processes: [process]);
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection",
            null,
            "test-system-id",
            config,
            true,
            null,
            _clock.Now);

        _db.AddAzureDevOpsBoardsConnection(connection);
        return connection;
    }

    [Fact]
    public async Task Handle_WhenIntegrationInternalIdIsMissingFromRegistrations_ClearsDanglingPointer()
    {
        // Arrange — connection has an IntegrationState pointing at internalId, but the
        // registrations list (sourced from Wayd.Work) is empty -> the WorkProcess row was deleted.
        var externalId = Guid.CreateVersion7();
        var internalId = Guid.CreateVersion7();
        var connection = CreateConnectionWithProcess(externalId, internalId);

        var azdoProcess = AzureDevOpsBoardsWorkProcess.Create(externalId, "Agile", "test");

        var command = new SyncAzureDevOpsConnectionConfigurationCommand(
            connection.Id,
            [azdoProcess],
            [],
            [], // no live registrations
            []);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var process = connection.Configuration.WorkProcesses.Single();
        process.IntegrationState.Should().BeNull();
        process.HasIntegration.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_WhenIntegrationInternalIdMatchesRegistration_LeavesItIntact()
    {
        // Arrange — registrations list contains the live registration; nothing should be cleared.
        var externalId = Guid.CreateVersion7();
        var internalId = Guid.CreateVersion7();
        var connection = CreateConnectionWithProcess(externalId, internalId);

        var azdoProcess = AzureDevOpsBoardsWorkProcess.Create(externalId, "Agile", "test");

        var liveRegistration = new IntegrationRegistration<Guid, Guid>(
            externalId,
            IntegrationState<Guid>.Create(internalId, true));

        var command = new SyncAzureDevOpsConnectionConfigurationCommand(
            connection.Id,
            [azdoProcess],
            [],
            [liveRegistration],
            []);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var process = connection.Configuration.WorkProcesses.Single();
        process.IntegrationState.Should().NotBeNull();
        process.IntegrationState!.InternalId.Should().Be(internalId);
    }

    [Fact]
    public async Task Handle_WhenLiveRegistrationHasDifferentInternalId_RebindsToLiveRegistration()
    {
        // Arrange — the underlying Wayd.Work.WorkProcess was deleted and recreated
        // (same ExternalId, new InternalId). The handler should rebind the connection
        // to the live registration rather than leave the stale InternalId in place.
        var externalId = Guid.CreateVersion7();
        var staleInternalId = Guid.CreateVersion7();
        var connection = CreateConnectionWithProcess(externalId, staleInternalId);

        var azdoProcess = AzureDevOpsBoardsWorkProcess.Create(externalId, "Agile", "test");

        var liveInternalId = Guid.CreateVersion7();
        var liveRegistration = new IntegrationRegistration<Guid, Guid>(
            externalId,
            IntegrationState<Guid>.Create(liveInternalId, true));

        var command = new SyncAzureDevOpsConnectionConfigurationCommand(
            connection.Id,
            [azdoProcess],
            [],
            [liveRegistration],
            []);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var process = connection.Configuration.WorkProcesses.Single();
        process.IntegrationState.Should().NotBeNull();
        process.IntegrationState!.InternalId.Should().Be(liveInternalId);
    }

    [Fact]
    public async Task Handle_WhenNoIntegrationState_DoesNotInvokeHealing()
    {
        // Arrange — work process has never been integrated; healing path should be a no-op.
        var externalId = Guid.CreateVersion7();
        var process = AzureDevOpsBoardsWorkProcess.Create(externalId, "Agile", "test");
        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT", processes: [process]);
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection", null, "test-system-id", config, true, null, _clock.Now);
        _db.AddAzureDevOpsBoardsConnection(connection);

        var azdoProcess = AzureDevOpsBoardsWorkProcess.Create(externalId, "Agile", "test");

        var command = new SyncAzureDevOpsConnectionConfigurationCommand(
            connection.Id,
            [azdoProcess],
            [],
            [],
            []);

        // Act
        var result = await _sut.Handle(command, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.WorkProcesses.Single().IntegrationState.Should().BeNull();
    }
}
