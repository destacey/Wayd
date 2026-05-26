using CSharpFunctionalExtensions;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Wayd.AppIntegration.Application.Connections.Commands;
using Wayd.AppIntegration.Application.Connections.Commands.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Managers;
using Wayd.AppIntegration.Application.Connections.Queries.AzureDevOps;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Models;
using Moq;
using Moq.AutoMock;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Managers;

public class AzureDevOpsInitManagerTests
{
    private readonly AutoMocker _mocker;
    private readonly AzureDevOpsInitManager _sut;

    public AzureDevOpsInitManagerTests()
    {
        _mocker = new AutoMocker();
        _mocker.Use<ILogger<AzureDevOpsInitManager>>(Mock.Of<ILogger<AzureDevOpsInitManager>>());
        _sut = _mocker.CreateInstance<AzureDevOpsInitManager>();
    }

    #region Helpers

    private static AzureDevOpsConnectionDetailsDto CreateConnectionDetails(
        Guid connectionId,
        string? systemId = "test-system-id",
        bool isValidConfiguration = true,
        bool isActive = true,
        List<AzureDevOpsWorkProcessDto>? workProcesses = null,
        List<AzureDevOpsWorkspaceDto>? workspaces = null)
    {
        return new AzureDevOpsConnectionDetailsDto
        {
            Id = connectionId,
            Name = "Test Connection",
            Connector = new Common.Application.Dtos.SimpleNavigationDto { Id = (int)Connector.AzureDevOps, Name = "Azure DevOps" },
            Category = new Common.Application.Dtos.SimpleNavigationDto { Id = (int)ConnectorCategory.WorkSync, Name = "Work Sync" },
            IsActive = isActive,
            IsValidConfiguration = isValidConfiguration,
            SystemId = systemId,
            Configuration = new AzureDevOpsConnectionConfigurationDto
            {
                Organization = "TestOrg",
                PersonalAccessToken = "test-pat",
                OrganizationUrl = "https://dev.azure.com/TestOrg",
                WorkProcesses = workProcesses ?? [],
                Workspaces = workspaces ?? []
            },
            TeamConfiguration = new AzureDevOpsTeamConfigurationDto
            {
                WorkspaceTeams = []
            }
        };
    }

    private void SetupConnectionQuery(Guid connectionId, AzureDevOpsConnectionDetailsDto? details)
    {
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.Is<GetAzureDevOpsConnectionQuery>(q => q.ConnectionId == connectionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
    }

    #endregion

    #region SyncOrganizationConfiguration

    [Fact]
    public async Task SyncOrganizationConfiguration_WhenConnectionNotFound_ReturnsFailure()
    {
        // Arrange
        var connectionId = Guid.CreateVersion7();
        SetupConnectionQuery(connectionId, null);

        // Act
        var result = await _sut.SyncOrganizationConfiguration(connectionId, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(connectionId.ToString());
    }

    [Fact]
    public async Task SyncOrganizationConfiguration_WhenInvalidConfiguration_ReturnsFailure()
    {
        // Arrange
        var connectionId = Guid.CreateVersion7();
        var details = CreateConnectionDetails(connectionId, isValidConfiguration: false);
        SetupConnectionQuery(connectionId, details);

        // Act
        var result = await _sut.SyncOrganizationConfiguration(connectionId, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not valid");
    }

    [Fact]
    public async Task SyncOrganizationConfiguration_WhenTestConnectionFails_ReturnsFailure()
    {
        // Arrange
        var connectionId = Guid.CreateVersion7();
        var details = CreateConnectionDetails(connectionId);
        SetupConnectionQuery(connectionId, details);

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.TestConnection(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Failure("Connection refused"));

        // Act
        var result = await _sut.SyncOrganizationConfiguration(connectionId, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Unable to connect");
    }

    [Fact]
    public async Task SyncOrganizationConfiguration_WhenGetWorkProcessesFails_ReturnsFailure()
    {
        // Arrange
        var connectionId = Guid.CreateVersion7();
        var details = CreateConnectionDetails(connectionId);
        SetupConnectionQuery(connectionId, details);

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.TestConnection(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkProcesses(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<List<IExternalWorkProcess>>("Failed to get processes"));

        // Act
        var result = await _sut.SyncOrganizationConfiguration(connectionId, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SyncOrganizationConfiguration_WhenGetWorkspacesFails_ReturnsFailure()
    {
        // Arrange
        var connectionId = Guid.CreateVersion7();
        var details = CreateConnectionDetails(connectionId);
        SetupConnectionQuery(connectionId, details);

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.TestConnection(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkProcesses(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IExternalWorkProcess>()));

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkspaces(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<List<IExternalWorkspace>>("Failed to get workspaces"));

        // Act
        var result = await _sut.SyncOrganizationConfiguration(connectionId, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SyncOrganizationConfiguration_WhenGetTeamsFails_ReturnsFailure()
    {
        // Arrange
        var connectionId = Guid.CreateVersion7();
        var details = CreateConnectionDetails(connectionId);
        SetupConnectionQuery(connectionId, details);

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.TestConnection(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        var mockProcess = new Mock<IExternalWorkProcess>();
        mockProcess.Setup(p => p.Id).Returns(Guid.CreateVersion7());
        mockProcess.Setup(p => p.Name).Returns("Agile");
        mockProcess.Setup(p => p.WorkspaceIds).Returns([Guid.CreateVersion7()]);

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkProcesses(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IExternalWorkProcess> { mockProcess.Object }));

        var wsId = mockProcess.Object.WorkspaceIds.First();
        var mockWorkspace = new Mock<IExternalWorkspace>();
        mockWorkspace.Setup(w => w.Id).Returns(wsId);
        mockWorkspace.Setup(w => w.Name).Returns("TestProject");

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkspaces(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IExternalWorkspace> { mockWorkspace.Object }));

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<GetIntegrationRegistrationsForWorkProcessesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IntegrationRegistration<Guid, Guid>>());

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetTeams(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<List<IExternalTeam>>("Failed to get teams"));

        // Act
        var result = await _sut.SyncOrganizationConfiguration(connectionId, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task SyncOrganizationConfiguration_HappyPath_Succeeds()
    {
        // Arrange
        var connectionId = Guid.CreateVersion7();
        var details = CreateConnectionDetails(connectionId);
        SetupConnectionQuery(connectionId, details);

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.TestConnection(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkProcesses(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IExternalWorkProcess>()));

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkspaces(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IExternalWorkspace>()));

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<GetIntegrationRegistrationsForWorkProcessesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IntegrationRegistration<Guid, Guid>>());

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<SyncAzureDevOpsConnectionConfigurationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.SyncOrganizationConfiguration(connectionId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mocker.GetMock<ISender>()
            .Verify(s => s.Send(It.IsAny<SyncAzureDevOpsConnectionConfigurationCommand>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncOrganizationConfiguration_WithNoWorkspaces_SkipsTeamLoading()
    {
        // Arrange
        var connectionId = Guid.CreateVersion7();
        var details = CreateConnectionDetails(connectionId);
        SetupConnectionQuery(connectionId, details);

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.TestConnection(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkProcesses(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IExternalWorkProcess>()));

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkspaces(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IExternalWorkspace>()));

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<GetIntegrationRegistrationsForWorkProcessesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IntegrationRegistration<Guid, Guid>>());

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<SyncAzureDevOpsConnectionConfigurationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.SyncOrganizationConfiguration(connectionId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetTeams(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncOrganizationConfiguration_WhenExceptionThrown_ReturnsFailure()
    {
        // Arrange
        var connectionId = Guid.CreateVersion7();
        var details = CreateConnectionDetails(connectionId);
        SetupConnectionQuery(connectionId, details);

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.TestConnection(It.IsAny<string>(), It.IsAny<string>()))
            .ThrowsAsync(new InvalidOperationException("Unexpected error"));

        // Act
        var result = await _sut.SyncOrganizationConfiguration(connectionId, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    #endregion

    #region InitWorkProcessIntegration

    [Fact]
    public async Task InitWorkProcessIntegration_WhenConnectionNotFound_ReturnsFailure()
    {
        // Arrange
        var connectionId = Guid.CreateVersion7();
        var wpExternalId = Guid.CreateVersion7();
        SetupConnectionQuery(connectionId, null);

        // Act
        var result = await _sut.InitWorkProcessIntegration(connectionId, wpExternalId, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task InitWorkProcessIntegration_WhenInvalidConnection_ReturnsFailure()
    {
        // Arrange
        var connectionId = Guid.CreateVersion7();
        var wpExternalId = Guid.CreateVersion7();
        var details = CreateConnectionDetails(connectionId, isValidConfiguration: false);
        SetupConnectionQuery(connectionId, details);

        // Act
        var result = await _sut.InitWorkProcessIntegration(connectionId, wpExternalId, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task InitWorkProcessIntegration_WhenWorkProcessNotLinked_ReturnsFailure()
    {
        // Arrange
        var connectionId = Guid.CreateVersion7();
        var wpExternalId = Guid.CreateVersion7();
        var details = CreateConnectionDetails(connectionId,
            workProcesses: [new AzureDevOpsWorkProcessDto
            {
                ExternalId = Guid.CreateVersion7(), // different from wpExternalId
                Name = "Other Process"
            }]);
        SetupConnectionQuery(connectionId, details);

        // Act
        var result = await _sut.InitWorkProcessIntegration(connectionId, wpExternalId, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not linked");
    }

    [Fact]
    public async Task InitWorkProcessIntegration_WhenAlreadyIntegratedOnThisConnection_ReturnsFailure()
    {
        // Arrange - the connection already has an IntegrationState for this process
        var connectionId = Guid.CreateVersion7();
        var wpExternalId = Guid.CreateVersion7();
        var wpInternalId = Guid.CreateVersion7();
        var details = CreateConnectionDetails(connectionId,
            workProcesses: [new AzureDevOpsWorkProcessDto
            {
                ExternalId = wpExternalId,
                Name = "Agile",
                IntegrationState = new IntegrationStateDto { InternalId = wpInternalId, IsActive = true }
            }]);
        SetupConnectionQuery(connectionId, details);

        // Act
        var result = await _sut.InitWorkProcessIntegration(connectionId, wpExternalId, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already integrated");
    }

    [Fact]
    public async Task InitWorkProcessIntegration_WhenProcessExistsOnAnotherConnection_ReusesExistingWorkProcess()
    {
        // Arrange - Process exists globally (another connection initialized it)
        // but this connection does NOT have an IntegrationState for it yet
        var connectionId = Guid.CreateVersion7();
        var wpExternalId = Guid.CreateVersion7();
        var wpInternalId = Guid.CreateVersion7();

        var details = CreateConnectionDetails(connectionId,
            workProcesses: [new AzureDevOpsWorkProcessDto { ExternalId = wpExternalId, Name = "Agile" }]);
        SetupConnectionQuery(connectionId, details);

        // ExternalWorkProcessExistsQuery - returns true (another connection has it)
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<ExternalWorkProcessExistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // GetIntegrationRegistrationsForWorkProcessesQuery - returns the existing registration
        var existingIntegrationState = IntegrationState<Guid>.Create(wpInternalId, true);
        var existingRegistration = new IntegrationRegistration<Guid, Guid>(wpExternalId, existingIntegrationState);
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.Is<GetIntegrationRegistrationsForWorkProcessesQuery>(q => q.ExternalId == wpExternalId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IntegrationRegistration<Guid, Guid>> { existingRegistration });

        // UpdateAzureDevOpsWorkProcessIntegrationStateCommand - links this connection
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<UpdateAzureDevOpsWorkProcessIntegrationStateCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.InitWorkProcessIntegration(connectionId, wpExternalId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(wpInternalId);

        // Should NOT create a new work process
        _mocker.GetMock<ISender>()
            .Verify(s => s.Send(It.IsAny<CreateExternalWorkProcessCommand>(), It.IsAny<CancellationToken>()), Times.Never);

        // Should NOT call Azure DevOps to get the process details (no need to sync types/statuses/workflows)
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetWorkProcess(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);

        // Should link this connection to the existing work process
        _mocker.GetMock<ISender>()
            .Verify(s => s.Send(It.Is<UpdateAzureDevOpsWorkProcessIntegrationStateCommand>(c =>
                c.ConnectionId == connectionId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task InitWorkProcessIntegration_HappyPath_Succeeds()
    {
        // Arrange
        var connectionId = Guid.CreateVersion7();
        var wpExternalId = Guid.CreateVersion7();
        var wpInternalId = Guid.CreateVersion7();

        var details = CreateConnectionDetails(connectionId,
            workProcesses: [new AzureDevOpsWorkProcessDto { ExternalId = wpExternalId, Name = "Agile" }]);
        SetupConnectionQuery(connectionId, details);

        // ExternalWorkProcessExistsQuery
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<ExternalWorkProcessExistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // SyncOrganizationConfiguration dependencies (called internally)
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.TestConnection(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkProcesses(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IExternalWorkProcess>()));
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkspaces(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IExternalWorkspace>()));
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<GetIntegrationRegistrationsForWorkProcessesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IntegrationRegistration<Guid, Guid>>());
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<SyncAzureDevOpsConnectionConfigurationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // GetWorkProcess (external)
        var mockWpConfig = new Mock<IExternalWorkProcessConfiguration>();
        mockWpConfig.Setup(p => p.Name).Returns("Agile");
        mockWpConfig.Setup(p => p.WorkTypes).Returns(new List<IExternalWorkTypeWorkflow>());
        mockWpConfig.Setup(p => p.WorkStatuses).Returns(new List<IExternalWorkStatus>());
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkProcess(It.IsAny<string>(), It.IsAny<string>(), wpExternalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(mockWpConfig.Object));

        // CreateExternalWorkProcessCommand
        var integrationState = IntegrationState<Guid>.Create(wpInternalId, true);
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<CreateExternalWorkProcessCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(integrationState));

        // UpdateAzureDevOpsWorkProcessIntegrationStateCommand
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<UpdateAzureDevOpsWorkProcessIntegrationStateCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.InitWorkProcessIntegration(connectionId, wpExternalId, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(wpInternalId);
    }

    #endregion

    #region InitWorkspaceIntegration

    [Fact]
    public async Task InitWorkspaceIntegration_WhenConnectionNotFound_ReturnsFailure()
    {
        // Arrange
        var connectionId = Guid.CreateVersion7();
        var wsExternalId = Guid.CreateVersion7();
        SetupConnectionQuery(connectionId, null);

        // Act
        var result = await _sut.InitWorkspaceIntegration(connectionId, wsExternalId, "TESTWS", "Test Workspace", null, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task InitWorkspaceIntegration_WhenWorkspaceNotLinked_ReturnsFailure()
    {
        // Arrange
        var connectionId = Guid.CreateVersion7();
        var wsExternalId = Guid.CreateVersion7();
        var details = CreateConnectionDetails(connectionId,
            workspaces: [new AzureDevOpsWorkspaceDto
            {
                ExternalId = Guid.CreateVersion7(), // different
                Name = "Other Workspace",
                WorkProcessId = Guid.CreateVersion7()
            }]);
        SetupConnectionQuery(connectionId, details);

        // Act
        var result = await _sut.InitWorkspaceIntegration(connectionId, wsExternalId, "TESTWS", "Test Workspace", null, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not linked");
    }

    [Fact]
    public async Task InitWorkspaceIntegration_WhenAlreadyIntegrated_ReturnsFailure()
    {
        // Arrange
        var connectionId = Guid.CreateVersion7();
        var wsExternalId = Guid.CreateVersion7();
        var details = CreateConnectionDetails(connectionId,
            workspaces: [new AzureDevOpsWorkspaceDto { ExternalId = wsExternalId, Name = "TestProject", WorkProcessId = Guid.CreateVersion7() }]);
        SetupConnectionQuery(connectionId, details);

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<ExternalWorkspaceExistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.InitWorkspaceIntegration(connectionId, wsExternalId, "TESTWS", "Test Workspace", null, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already integrated");
    }

    [Fact]
    public async Task InitWorkspaceIntegration_WhenDuplicateKey_ReturnsFailure()
    {
        // Arrange
        var connectionId = Guid.CreateVersion7();
        var wsExternalId = Guid.CreateVersion7();
        var details = CreateConnectionDetails(connectionId,
            workspaces: [new AzureDevOpsWorkspaceDto { ExternalId = wsExternalId, Name = "TestProject", WorkProcessId = Guid.CreateVersion7() }]);
        SetupConnectionQuery(connectionId, details);

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<ExternalWorkspaceExistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<WorkspaceKeyExistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        // Act
        var result = await _sut.InitWorkspaceIntegration(connectionId, wsExternalId, "TESTWS", "Test Workspace", null, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("already in use");
    }

    [Fact]
    public async Task InitWorkspaceIntegration_WhenMissingSystemId_ReturnsFailure()
    {
        // Arrange
        var connectionId = Guid.CreateVersion7();
        var wsExternalId = Guid.CreateVersion7();
        var details = CreateConnectionDetails(connectionId, systemId: null,
            workspaces: [new AzureDevOpsWorkspaceDto { ExternalId = wsExternalId, Name = "TestProject", WorkProcessId = Guid.CreateVersion7() }]);
        SetupConnectionQuery(connectionId, details);

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<ExternalWorkspaceExistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<WorkspaceKeyExistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // SyncOrganizationConfiguration dependencies
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.TestConnection(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkProcesses(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IExternalWorkProcess>()));
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkspaces(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IExternalWorkspace>()));
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<GetIntegrationRegistrationsForWorkProcessesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IntegrationRegistration<Guid, Guid>>());
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<SyncAzureDevOpsConnectionConfigurationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.InitWorkspaceIntegration(connectionId, wsExternalId, "TESTWS", "Test Workspace", null, CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("systemId");
    }

    [Fact]
    public async Task InitWorkspaceIntegration_HappyPath_Succeeds()
    {
        // Arrange
        var connectionId = Guid.CreateVersion7();
        var wsExternalId = Guid.CreateVersion7();
        var wsInternalId = Guid.CreateVersion7();
        var details = CreateConnectionDetails(connectionId,
            workspaces: [new AzureDevOpsWorkspaceDto { ExternalId = wsExternalId, Name = "TestProject", WorkProcessId = Guid.CreateVersion7() }]);
        SetupConnectionQuery(connectionId, details);

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<ExternalWorkspaceExistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<WorkspaceKeyExistsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        // SyncOrganizationConfiguration dependencies
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.TestConnection(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Result.Success());
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkProcesses(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IExternalWorkProcess>()));
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkspaces(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IExternalWorkspace>()));
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<GetIntegrationRegistrationsForWorkProcessesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IntegrationRegistration<Guid, Guid>>());
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<SyncAzureDevOpsConnectionConfigurationCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // GetWorkspace (external)
        var mockWsConfig = new Mock<IExternalWorkspaceConfiguration>();
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkspace(It.IsAny<string>(), It.IsAny<string>(), wsExternalId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(mockWsConfig.Object));

        // CreateExternalWorkspaceCommand
        var integrationState = IntegrationState<Guid>.Create(wsInternalId, true);
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<CreateExternalWorkspaceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(integrationState));

        // UpdateAzureDevOpsWorkspaceIntegrationStateCommand
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<UpdateAzureDevOpsWorkspaceIntegrationStateCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // Act
        var result = await _sut.InitWorkspaceIntegration(connectionId, wsExternalId, "TESTWS", "Test Workspace", null, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(wsInternalId);
    }

    #endregion
}
