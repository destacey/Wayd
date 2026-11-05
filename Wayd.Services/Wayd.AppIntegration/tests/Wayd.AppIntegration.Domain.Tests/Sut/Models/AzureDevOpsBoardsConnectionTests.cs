using FluentAssertions;
using Wayd.AppIntegration.Domain.Interfaces;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models;
using Wayd.Tests.Shared;

namespace Wayd.AppIntegration.Domain.Tests.Sut.Models;

public class AzureDevOpsBoardsConnectionTests
{
    private static readonly EventActor Actor = EventActor.User(Guid.NewGuid().ToString());

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
            Actor, _dateTimeProvider.Now);

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
            Actor, _dateTimeProvider.Now);

        // Assert
        var syncable = connection as ISyncableConnection;
        syncable.Should().NotBeNull();
        syncable!.SystemId.Should().Be("test-system-id");
        // CanSync is false because the connection has no active integration objects yet.
        syncable.CanSync.Should().BeFalse();
    }

    [Fact]
    public void CanSync_ShouldBeFalse_WhenOnlyAProcessIsSetUp()
    {
        // Arrange — a process with no syncing project gives a sync nothing to read
        var workProcess = AzureDevOpsBoardsWorkProcess.Create(Guid.CreateVersion7(), "Agile", null);
        workProcess.AddIntegrationState(IntegrationState<Guid>.Create(Guid.CreateVersion7(), true));

        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT", processes: [workProcess]);

        // Act
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection", null, "test-system-id", config, true, null, Actor, _dateTimeProvider.Now);

        // Assert
        ((ISyncableConnection)connection).CanSync.Should().BeFalse();
    }

    [Fact]
    public void CanSync_ShouldBeFalse_WhenDeactivated()
    {
        // Arrange
        var workspace = AzureDevOpsBoardsWorkspace.Create(Guid.CreateVersion7(), "Project", null, Guid.CreateVersion7());
        workspace.AddIntegrationState(IntegrationState<Guid>.Create(Guid.CreateVersion7(), true));

        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT", workspaces: [workspace]);
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection", null, "test-system-id", config, true, null, Actor, _dateTimeProvider.Now);

        // Sanity: with a syncing project + active connection, CanSync should be true.
        ((ISyncableConnection)connection).CanSync.Should().BeTrue();

        // Act
        connection.Deactivate(ConnectionActivatableArgs.Create(Actor, _dateTimeProvider.Now));

        // Assert
        ((ISyncableConnection)connection).CanSync.Should().BeFalse();
    }

    [Fact]
    public void CanSync_ShouldBeFalse_WhenConfigurationInvalid()
    {
        // Arrange
        var workspace = AzureDevOpsBoardsWorkspace.Create(Guid.CreateVersion7(), "Project", null, Guid.CreateVersion7());
        workspace.AddIntegrationState(IntegrationState<Guid>.Create(Guid.CreateVersion7(), true));

        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT", workspaces: [workspace]);
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection", null, "test-system-id", config, configurationIsValid: false, null, Actor, _dateTimeProvider.Now);

        // Assert
        ((ISyncableConnection)connection).CanSync.Should().BeFalse();
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
            "Test Connection", null, "test-system-id", config, true, null, Actor, _dateTimeProvider.Now);

        var liveInternalId = Guid.CreateVersion7();
        var registration = new IntegrationRegistration<Guid, Guid>(
            externalId,
            IntegrationState<Guid>.Create(liveInternalId, true));

        var result = connection.UpdateWorkProcessIntegrationState(registration, _dateTimeProvider.Now);

        result.IsSuccess.Should().BeTrue();
        connection.Configuration.WorkProcesses.Single().IntegrationState!.InternalId.Should().Be(liveInternalId);
    }

    [Fact]
    public void SetWorkspaceSync_WhenSetUp_TurnsSyncOffAndOn()
    {
        // Arrange
        var externalId = Guid.CreateVersion7();
        var workspace = AzureDevOpsBoardsWorkspace.Create(externalId, "Project", null, Guid.CreateVersion7());
        workspace.AddIntegrationState(IntegrationState<Guid>.Create(Guid.CreateVersion7(), true));

        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT", workspaces: [workspace]);
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection", null, "test-system-id", config, true, null, Actor, _dateTimeProvider.Now);

        // Act
        var offResult = connection.SetWorkspaceSync(externalId, false);
        var isActiveAfterOff = workspace.IntegrationIsActive;
        var onResult = connection.SetWorkspaceSync(externalId, true);

        // Assert
        offResult.IsSuccess.Should().BeTrue();
        isActiveAfterOff.Should().BeFalse();
        onResult.IsSuccess.Should().BeTrue();
        workspace.IntegrationIsActive.Should().BeTrue();
    }

    [Fact]
    public void SetWorkspaceSync_WhenNotSetUp_ShouldFail()
    {
        // Arrange
        var externalId = Guid.CreateVersion7();
        var workspace = AzureDevOpsBoardsWorkspace.Create(externalId, "Project", null, Guid.CreateVersion7());

        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT", workspaces: [workspace]);
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection", null, "test-system-id", config, true, null, Actor, _dateTimeProvider.Now);

        // Act
        var result = connection.SetWorkspaceSync(externalId, true);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("has not been set up");
        workspace.HasIntegration.Should().BeFalse();
    }

    [Fact]
    public void SetWorkspaceSync_WhenWorkspaceNotFound_ShouldFail()
    {
        // Arrange
        var config = new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "TestPAT");
        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection", null, "test-system-id", config, true, null, Actor, _dateTimeProvider.Now);

        // Act
        var result = connection.SetWorkspaceSync(Guid.CreateVersion7(), true);

        // Assert
        result.IsFailure.Should().BeTrue();
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
            Actor, _dateTimeProvider.Now);

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
            Actor, _dateTimeProvider.Now);

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
            Actor, _dateTimeProvider.Now);

        // Act
        var result = connection.ClearWorkProcessIntegrationState(Guid.CreateVersion7(), _dateTimeProvider.Now);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Unable to find work process");
    }
}
