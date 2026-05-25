using FluentAssertions;
using Wayd.AppIntegration.Domain.Interfaces;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Domain.Models;
using Wayd.Tests.Shared;

namespace Wayd.AppIntegration.Domain.Tests.Sut.Models;

public class AzureDevOpsBoardsConnectionTests
{
    private readonly TestingDateTimeProvider _dateTimeProvider;

    public AzureDevOpsBoardsConnectionTests()
    {
        _dateTimeProvider = new(new DateTime(2026, 02, 10, 12, 0, 0));
    }

    [Fact]
    public void Create_ShouldImplement_ISyncableConnection()
    {
        // Arrange & Act
        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT");
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection",
            "Test Description",
            "test-system-id",
            config,
            true,
            null,
            _dateTimeProvider.Now);

        // Assert
        connection.Should().BeAssignableTo<ISyncableConnection>();
    }

    [Fact]
    public void Create_ShouldInitialize_SyncProperties()
    {
        // Arrange
        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT");

        // Act
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection",
            "Test Description",
            "test-system-id",
            config,
            true,
            null,
            _dateTimeProvider.Now);

        // Assert
        var syncable = connection as ISyncableConnection;
        syncable.Should().NotBeNull();
        syncable!.SystemId.Should().Be("test-system-id");
        syncable.IsSyncEnabled.Should().BeFalse();
        syncable.CanSync.Should().BeFalse();
    }

    [Fact]
    public void SetSyncState_WhenEnabled_ShouldRequireValidConfiguration()
    {
        // Arrange
        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT");
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection",
            null,
            "test-system-id",
            config,
            false, // Invalid configuration
            null,
            _dateTimeProvider.Now);

        // Act
        var result = connection.SetSyncState(true, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Configuration is invalid");
    }

    [Fact]
    public void SetSyncState_WhenEnabled_ShouldRequireActiveIntegrationObjects()
    {
        // Arrange
        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT");
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection",
            null,
            "test-system-id",
            config,
            true, // Valid configuration
            null,
            _dateTimeProvider.Now);

        // Act
        var result = connection.SetSyncState(true, _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("No active integration objects");
    }

    [Fact]
    public void SetSyncState_WhenDisabled_ShouldSucceed()
    {
        // Arrange
        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT");
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection",
            null,
            "test-system-id",
            config,
            false,
            null,
            _dateTimeProvider.Now);

        // Act
        var result = connection.SetSyncState(false, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var syncable = connection as ISyncableConnection;
        syncable!.IsSyncEnabled.Should().BeFalse();
    }

    [Fact]
    public void CanSync_ShouldBeFalse_WhenNotActive()
    {
        // Arrange
        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT");
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection",
            null,
            "test-system-id",
            config,
            true,
            null,
            _dateTimeProvider.Now);

        connection.Deactivate(_dateTimeProvider.Now);

        // Act
        var syncable = connection as ISyncableConnection;

        // Assert
        syncable!.CanSync.Should().BeFalse();
    }

    [Fact]
    public void UpdateWorkProcessIntegrationState_WhenInternalIdDiffers_RebindsToNewState()
    {
        // The underlying Wayd.Work.WorkProcess was deleted and recreated with a new InternalId.
        // The connection should rebind to the live registration rather than keep the stale id.
        var externalId = Guid.CreateVersion7();
        var staleInternalId = Guid.CreateVersion7();
        var workProcess = AzureDevOpsBoardsWorkProcess.Create(externalId, "Agile", null);
        workProcess.AddIntegrationState(IntegrationState<Guid>.Create(staleInternalId, true));

        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT", processes: [workProcess]);
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection", null, "test-system-id", config, true, null, _dateTimeProvider.Now);

        var liveInternalId = Guid.CreateVersion7();
        var registration = new IntegrationRegistration<Guid, Guid>(
            externalId,
            IntegrationState<Guid>.Create(liveInternalId, true));

        var result = connection.UpdateWorkProcessIntegrationState(registration, _dateTimeProvider.Now);

        result.IsSuccess.Should().BeTrue();
        connection.Configuration.WorkProcesses.Single().IntegrationState!.InternalId.Should().Be(liveInternalId);
    }

    [Fact]
    public void ClearWorkProcessIntegrationState_WhenIntegrationExists_ShouldRemoveIt()
    {
        // Arrange
        var externalId = Guid.CreateVersion7();
        var internalId = Guid.CreateVersion7();
        var workProcess = AzureDevOpsBoardsWorkProcess.Create(externalId, "Agile", null);
        workProcess.AddIntegrationState(IntegrationState<Guid>.Create(internalId, true));

        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT", processes: [workProcess]);
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection",
            null,
            "test-system-id",
            config,
            true,
            null,
            _dateTimeProvider.Now);

        // Act
        var result = connection.ClearWorkProcessIntegrationState(externalId, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var process = connection.Configuration.WorkProcesses.Single();
        process.IntegrationState.Should().BeNull();
        process.HasIntegration.Should().BeFalse();
    }

    [Fact]
    public void ClearWorkProcessIntegrationState_WhenNoIntegration_ShouldNoOp()
    {
        // Arrange
        var externalId = Guid.CreateVersion7();
        var workProcess = AzureDevOpsBoardsWorkProcess.Create(externalId, "Agile", null);

        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT", processes: [workProcess]);
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection",
            null,
            "test-system-id",
            config,
            true,
            null,
            _dateTimeProvider.Now);

        // Act
        var result = connection.ClearWorkProcessIntegrationState(externalId, _dateTimeProvider.Now);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.WorkProcesses.Single().IntegrationState.Should().BeNull();
    }

    [Fact]
    public void ClearWorkProcessIntegrationState_WhenWorkProcessNotFound_ShouldFail()
    {
        // Arrange
        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT");
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection",
            null,
            "test-system-id",
            config,
            true,
            null,
            _dateTimeProvider.Now);

        // Act
        var result = connection.ClearWorkProcessIntegrationState(Guid.CreateVersion7(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Unable to find work process");
    }

    [Fact]
    public void CanSync_ShouldBeFalse_WhenSyncDisabled()
    {
        // Arrange
        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT");
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection",
            null,
            "test-system-id",
            config,
            true,
            null,
            _dateTimeProvider.Now);

        // Act
        var syncable = connection as ISyncableConnection;

        // Assert
        syncable!.IsSyncEnabled.Should().BeFalse();
        syncable.CanSync.Should().BeFalse();
    }
}
