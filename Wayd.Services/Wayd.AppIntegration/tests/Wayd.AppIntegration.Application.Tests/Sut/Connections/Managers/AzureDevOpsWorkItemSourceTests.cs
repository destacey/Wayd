using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Dtos;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Managers;
using Wayd.AppIntegration.Application.Connections.Queries;
using Wayd.AppIntegration.Application.Connections.Queries.AzureDevOps;
using Wayd.AppIntegration.Application.Connectors.Dtos;
using Wayd.AppIntegration.Application.Interfaces;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Enums;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Application.Requests.WorkManagement.Interfaces;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Abstractions;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Managers;

/// <summary>
/// Adapter-level tests for <see cref="AzureDevOpsWorkItemSource"/>. Covers bind validation, plan
/// flattening, work-process dedup, partial-failure semantics, and SyncType branching — the
/// behaviour the source must preserve from the deleted <c>AzureDevOpsSyncManager</c>.
/// </summary>
public class AzureDevOpsWorkItemSourceTests
{
    private readonly AutoMocker _mocker;
    private readonly AzureDevOpsWorkProcessSyncTracker _workProcessSyncTracker = new();
    private readonly AzureDevOpsWorkItemSource _sut;

    public AzureDevOpsWorkItemSourceTests()
    {
        _mocker = new AutoMocker();
        _mocker.Use<ILogger<AzureDevOpsWorkItemSource>>(Mock.Of<ILogger<AzureDevOpsWorkItemSource>>());
        _mocker.Use(_workProcessSyncTracker);
        _sut = _mocker.CreateInstance<AzureDevOpsWorkItemSource>();
    }

    #region Helpers

    private static SyncableConnectionDescriptor BuildDescriptor(
        Guid? connectionId = null,
        string? systemId = "system-1",
        AzureDevOpsBoardsConnectionConfiguration? cfg = null,
        AzureDevOpsBoardsTeamConfiguration? teamCfg = null)
    {
        return new SyncableConnectionDescriptor(
            ConnectionId: connectionId ?? Guid.CreateVersion7(),
            Connector: Connector.AzureDevOps,
            SystemId: systemId,
            Configuration: cfg ?? new AzureDevOpsBoardsConnectionConfiguration("TestOrg", "test-pat"),
            TeamConfiguration: teamCfg);
    }

    private static AzureDevOpsWorkProcessDto Process(Guid? externalId = null, Guid? internalId = null, bool isActive = true) =>
        new()
        {
            ExternalId = externalId ?? Guid.CreateVersion7(),
            Name = "Agile",
            Description = "desc",
            IntegrationState = isActive
                ? new IntegrationStateDto { InternalId = internalId ?? Guid.CreateVersion7(), IsActive = true }
                : null
        };

    private static AzureDevOpsWorkspaceDto Workspace(Guid workProcessExternalId, Guid? externalId = null, Guid? internalId = null, string name = "Project", bool isActive = true) =>
        new()
        {
            ExternalId = externalId ?? Guid.CreateVersion7(),
            Name = name,
            Description = "desc",
            WorkProcessId = workProcessExternalId,
            IntegrationState = isActive
                ? new IntegrationStateDto { InternalId = internalId ?? Guid.CreateVersion7(), IsActive = true }
                : null
        };

    private static AzureDevOpsConnectionDetailsDto BuildConnectionDetails(
        Guid connectionId,
        List<AzureDevOpsWorkProcessDto> processes,
        List<AzureDevOpsWorkspaceDto> workspaces,
        List<AzureDevOpsWorkspaceTeamDto>? teams = null,
        string systemId = "system-1") =>
        new()
        {
            Id = connectionId,
            Name = "Test Connection",
            Connector = new SimpleNavigationDto { Id = (int)Connector.AzureDevOps, Name = "Azure DevOps" },
            Capabilities = [ConnectorCapabilityDto.FromEnum(ConnectorCapability.WorkItems)],
            IsActive = true,
            IsValidConfiguration = true,
            SystemId = systemId,
            Configuration = new AzureDevOpsConnectionConfigurationDto
            {
                Organization = "TestOrg",
                PersonalAccessToken = "test-pat",
                OrganizationUrl = "https://dev.azure.com/TestOrg",
                WorkProcesses = processes,
                Workspaces = workspaces
            },
            TeamConfiguration = new AzureDevOpsTeamConfigurationDto { WorkspaceTeams = teams ?? [] }
        };

    private void StubWorkProcessSync()
    {
        var workProcessConfig = new Mock<IExternalWorkProcessConfiguration>();
        workProcessConfig.Setup(p => p.Name).Returns("Agile");
        workProcessConfig.Setup(p => p.WorkTypes).Returns(new List<IExternalWorkTypeWorkflow>());
        workProcessConfig.Setup(p => p.WorkStatuses).Returns(new List<IExternalWorkStatus>());

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkProcess(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workProcessConfig.Object));

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetWorkProcessSchemesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IWorkProcessSchemeDto>().AsReadOnly() as IReadOnlyList<IWorkProcessSchemeDto>);

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<UpdateExternalWorkProcessCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // SyncWorkspace
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkspace(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new Mock<IExternalWorkspaceConfiguration>().Object));

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<UpdateExternalWorkspaceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<ChangeExternalWorkspaceWorkProcessCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }

    private void StubWorkItemSubSteps()
    {
        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetWorkspaceWorkTypesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IWorkTypeDto>().AsReadOnly() as IReadOnlyList<IWorkTypeDto>));

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetWorkspaceMostRecentChangeDateQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<Instant?>(null));

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkItems(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string[]>(), It.IsAny<Dictionary<Guid, Guid?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IExternalWorkItem>()));

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetParentLinkChanges(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IExternalWorkItemLink>()));

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetDependencyLinkChanges(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IExternalWorkItemLink>()));

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetDeletedWorkItemIds(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Array.Empty<int>()));
    }

    #endregion

    // -------- Bind --------

    [Fact]
    public void Bind_RejectsWrongConnector()
    {
        var descriptor = new SyncableConnectionDescriptor(
            Guid.CreateVersion7(),
            Connector.Entra,
            SystemId: null,
            Configuration: new AzureDevOpsBoardsConnectionConfiguration("o", "p"),
            TeamConfiguration: null);

        var result = _sut.Bind(descriptor);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Entra");
    }

    [Fact]
    public void Bind_RejectsWrongConfigurationType()
    {
        var descriptor = new SyncableConnectionDescriptor(
            Guid.CreateVersion7(),
            Connector.AzureDevOps,
            SystemId: null,
            Configuration: "not-a-config",
            TeamConfiguration: null);

        var result = _sut.Bind(descriptor);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("AzureDevOpsBoardsConnectionConfiguration");
    }

    [Fact]
    public void Bind_AcceptsValidDescriptor()
    {
        var result = _sut.Bind(BuildDescriptor());
        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public void Connector_ReturnsAzureDevOps()
    {
        _sut.Connector.Should().Be(Connector.AzureDevOps);
    }

    // -------- GetSyncPlan --------

    [Fact]
    public async Task GetSyncPlan_EmitsFlatTargetsForActiveWorkspacesUnderActiveProcesses()
    {
        var descriptor = BuildDescriptor();
        _sut.Bind(descriptor);

        var processId = Guid.CreateVersion7();
        var workspaceId = Guid.CreateVersion7();
        var details = BuildConnectionDetails(
            descriptor.ConnectionId,
            processes: [Process(externalId: processId)],
            workspaces: [Workspace(processId, externalId: workspaceId, name: "ProjectA")]);

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.Is<GetAzureDevOpsConnectionQuery>(q => q.ConnectionId == descriptor.ConnectionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var result = await _sut.GetSyncPlan(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
        var target = result.Value.Single();
        target.ExternalWorkspaceId.Should().Be(workspaceId);
        target.WorkspaceName.Should().Be("ProjectA");
        target.Filters.AsDictionary.Should().ContainKey(AzureDevOpsWorkItemSource.TeamSettingsFilterKey);
        // Runner never sees "work process" as a concept — verify only by absence of process-shaped fields on the target.
        target.GetType().GetProperties().Select(p => p.Name).Should().NotContain("WorkProcessId");
    }

    [Fact]
    public async Task GetSyncPlan_IncludesWorkspacesWhoseProcessIsNotSetUp()
    {
        // Arrange — preparing the workspace sets the process up, rather than the plan skipping it
        var descriptor = BuildDescriptor();
        _sut.Bind(descriptor);

        var processId = Guid.CreateVersion7();
        var details = BuildConnectionDetails(
            descriptor.ConnectionId,
            processes: [Process(externalId: processId, isActive: false)],
            workspaces: [Workspace(processId)]);

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetAzureDevOpsConnectionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _sut.GetSyncPlan(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetSyncPlan_SkipsWorkspacesWhoseProcessIsDeactivatedInWayd()
    {
        // Arrange — deactivating a Wayd work process requires its workspaces to be deactivated
        // first, which does not reach the workspace's integration state
        var descriptor = BuildDescriptor();
        _sut.Bind(descriptor);

        var processId = Guid.CreateVersion7();
        var deactivatedProcess = Process(externalId: processId);
        deactivatedProcess.IntegrationState!.IsActive = false;
        var details = BuildConnectionDetails(
            descriptor.ConnectionId,
            processes: [deactivatedProcess],
            workspaces: [Workspace(processId)]);

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetAzureDevOpsConnectionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        // Act
        var result = await _sut.GetSyncPlan(CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSyncPlan_SkipsInactiveWorkspaces()
    {
        var descriptor = BuildDescriptor();
        _sut.Bind(descriptor);

        var processId = Guid.CreateVersion7();
        var details = BuildConnectionDetails(
            descriptor.ConnectionId,
            processes: [Process(externalId: processId)],
            workspaces: [Workspace(processId, isActive: false)]);

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetAzureDevOpsConnectionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var result = await _sut.GetSyncPlan(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeEmpty();
    }

    [Fact]
    public async Task GetSyncPlan_IncludesTeamSettingsForWorkspaceTeams()
    {
        var descriptor = BuildDescriptor();
        _sut.Bind(descriptor);

        var processId = Guid.CreateVersion7();
        var workspaceId = Guid.CreateVersion7();
        var teamId = Guid.CreateVersion7();
        var internalTeamId = Guid.CreateVersion7();
        var boardId = Guid.CreateVersion7();

        var details = BuildConnectionDetails(
            descriptor.ConnectionId,
            processes: [Process(externalId: processId)],
            workspaces: [Workspace(processId, externalId: workspaceId)],
            teams: [new AzureDevOpsWorkspaceTeamDto
            {
                WorkspaceId = workspaceId,
                TeamId = teamId,
                TeamName = "Alpha",
                BoardId = boardId,
                InternalTeamId = internalTeamId
            }]);

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetAzureDevOpsConnectionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var result = await _sut.GetSyncPlan(CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var target = result.Value.Single();
        target.Filters.TryGet(AzureDevOpsWorkItemSource.TeamSettingsFilterKey, out var settingsJson).Should().BeTrue();
        settingsJson.Should().Contain(teamId.ToString());
        settingsJson.Should().Contain(boardId.ToString());
    }

    [Fact]
    public async Task GetSyncPlan_ReturnsFailureWhenConnectionNotFound()
    {
        var descriptor = BuildDescriptor();
        _sut.Bind(descriptor);

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetAzureDevOpsConnectionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((AzureDevOpsConnectionDetailsDto?)null);

        var result = await _sut.GetSyncPlan(CancellationToken.None);

        result.IsFailure.Should().BeTrue();
    }

    // -------- PrepareWorkspaceForItemSync (work-process dedup) --------

    [Fact]
    public async Task PrepareWorkspaceForItemSync_OnlyCallsGetWorkProcessOncePerProcessAcrossMultipleWorkspaces()
    {
        var descriptor = BuildDescriptor();
        _sut.Bind(descriptor);

        var processId = Guid.CreateVersion7();
        var details = BuildConnectionDetails(
            descriptor.ConnectionId,
            processes: [Process(externalId: processId)],
            workspaces: [
                Workspace(processId, name: "ProjectA"),
                Workspace(processId, name: "ProjectB")
            ]);

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetAzureDevOpsConnectionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var planResult = await _sut.GetSyncPlan(CancellationToken.None);
        planResult.Value.Should().HaveCount(2);

        StubWorkProcessSync();

        var syncId = Guid.CreateVersion7();
        foreach (var target in planResult.Value)
        {
            var prepResult = await _sut.PrepareWorkspaceForItemSync(target, syncId, CancellationToken.None);
            prepResult.IsSuccess.Should().BeTrue();
        }

        // Same process across both workspaces: GetWorkProcess should only fire once.
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetWorkProcess(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        // GetWorkspace is per-workspace.
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetWorkspace(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task PrepareWorkspaceForItemSync_SyncsAProcessSharedByTwoConnectionsOncePerRun()
    {
        // Arrange — two connections to different organizations, both on a built-in process
        var processId = Guid.CreateVersion7();
        var firstDescriptor = BuildDescriptor();
        var secondDescriptor = BuildDescriptor();

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.Is<GetAzureDevOpsConnectionQuery>(q => q.ConnectionId == firstDescriptor.ConnectionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildConnectionDetails(firstDescriptor.ConnectionId, [Process(externalId: processId)], [Workspace(processId, name: "ProjectA")]));
        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.Is<GetAzureDevOpsConnectionQuery>(q => q.ConnectionId == secondDescriptor.ConnectionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildConnectionDetails(secondDescriptor.ConnectionId, [Process(externalId: processId)], [Workspace(processId, name: "ProjectB")]));
        StubWorkProcessSync();

        var secondSource = _mocker.CreateInstance<AzureDevOpsWorkItemSource>();
        var syncId = Guid.CreateVersion7();

        // Act
        foreach (var (source, descriptor) in new[] { (_sut, firstDescriptor), (secondSource, secondDescriptor) })
        {
            source.Bind(descriptor);
            var plan = await source.GetSyncPlan(CancellationToken.None);
            var prepResult = await source.PrepareWorkspaceForItemSync(plan.Value.Single(), syncId, CancellationToken.None);
            prepResult.IsSuccess.Should().BeTrue();
        }

        // Assert
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetWorkProcess(It.IsAny<AzureDevOpsConnectionContext>(), processId, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PrepareWorkspaceForItemSync_SyncsTheProcessAgainInANewRun()
    {
        var descriptor = BuildDescriptor();
        _sut.Bind(descriptor);

        var processId = Guid.CreateVersion7();
        var details = BuildConnectionDetails(
            descriptor.ConnectionId,
            processes: [Process(externalId: processId)],
            workspaces: [Workspace(processId)]);

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetAzureDevOpsConnectionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        StubWorkProcessSync();

        var plan1 = await _sut.GetSyncPlan(CancellationToken.None);
        await _sut.PrepareWorkspaceForItemSync(plan1.Value.Single(), Guid.CreateVersion7(), CancellationToken.None);

        // Rebind for the next run, which has its own sync id.
        _sut.Bind(descriptor);
        var plan2 = await _sut.GetSyncPlan(CancellationToken.None);
        await _sut.PrepareWorkspaceForItemSync(plan2.Value.Single(), Guid.CreateVersion7(), CancellationToken.None);

        // Each run syncs the work process once.
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetWorkProcess(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task PrepareWorkspaceForItemSync_WhenProcessNotSetUp_SetsItUpOnceAndAssignsTheWorkspace()
    {
        // Arrange
        var descriptor = BuildDescriptor();
        _sut.Bind(descriptor);

        var processId = Guid.CreateVersion7();
        var processInternalId = Guid.CreateVersion7();
        var workspaceA = Workspace(processId, name: "ProjectA");
        var workspaceB = Workspace(processId, name: "ProjectB");
        var details = BuildConnectionDetails(
            descriptor.ConnectionId,
            processes: [Process(externalId: processId, isActive: false)],
            workspaces: [workspaceA, workspaceB]);

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetAzureDevOpsConnectionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
        _mocker.GetMock<IAzureDevOpsInitManager>()
            .Setup(m => m.EnsureWorkProcessIntegration(descriptor.ConnectionId, processId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(processInternalId));
        StubWorkProcessSync();

        var plan = await _sut.GetSyncPlan(CancellationToken.None);

        // Act
        var syncId = Guid.CreateVersion7();
        var results = new List<Result>();
        foreach (var target in plan.Value)
            results.Add(await _sut.PrepareWorkspaceForItemSync(target, syncId, CancellationToken.None));

        // Assert
        results.Should().OnlyContain(r => r.IsSuccess);
        _mocker.GetMock<IAzureDevOpsInitManager>()
            .Verify(m => m.EnsureWorkProcessIntegration(descriptor.ConnectionId, processId, It.IsAny<CancellationToken>()), Times.Once);
        // Setting the process up just read it, so the run does not sync it again.
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetWorkProcess(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
        _mocker.GetMock<IDispatcher>()
            .Verify(s => s.Send(It.Is<ChangeExternalWorkspaceWorkProcessCommand>(c =>
                c.WorkspaceId == workspaceA.IntegrationState!.InternalId && c.NewWorkProcessExternalId == processId), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task PrepareWorkspaceForItemSync_WhenProcessCannotBeSetUp_FailsWithTheProjectName()
    {
        // Arrange
        var descriptor = BuildDescriptor();
        _sut.Bind(descriptor);

        var processId = Guid.CreateVersion7();
        var details = BuildConnectionDetails(
            descriptor.ConnectionId,
            processes: [Process(externalId: processId, isActive: false)],
            workspaces: [Workspace(processId, name: "ProjectA")]);

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetAzureDevOpsConnectionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
        _mocker.GetMock<IAzureDevOpsInitManager>()
            .Setup(m => m.EnsureWorkProcessIntegration(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<Guid>("The work process Agile is deactivated in Wayd."));

        var plan = await _sut.GetSyncPlan(CancellationToken.None);

        // Act
        var result = await _sut.PrepareWorkspaceForItemSync(plan.Value.Single(), Guid.CreateVersion7(), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("ProjectA").And.Contain("deactivated");
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetWorkProcess(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PrepareWorkspaceForItemSync_WhenProjectHasNoProcess_Fails()
    {
        // Arrange
        var descriptor = BuildDescriptor();
        _sut.Bind(descriptor);

        var details = BuildConnectionDetails(
            descriptor.ConnectionId,
            processes: [],
            workspaces: [Workspace(Guid.Empty, name: "ProjectA")]);

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetAzureDevOpsConnectionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var plan = await _sut.GetSyncPlan(CancellationToken.None);

        // Act
        var result = await _sut.PrepareWorkspaceForItemSync(plan.Value.Single(), Guid.CreateVersion7(), CancellationToken.None);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("no work process for project ProjectA");
        _mocker.GetMock<IAzureDevOpsInitManager>()
            .Verify(m => m.EnsureWorkProcessIntegration(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // -------- SyncWorkItems: SyncType branching --------

    [Fact]
    public async Task SyncWorkItems_Differential_CallsGetParentLinkChanges()
    {
        var descriptor = BuildDescriptor();
        _sut.Bind(descriptor);
        StubWorkItemSubSteps();

        var target = new WorkspaceSyncTarget(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Project", "key", WorkItemSyncFilters.Empty);

        var result = await _sut.SyncWorkItems(target, SyncType.Differential, Guid.CreateVersion7(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetParentLinkChanges(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncWorkItems_Full_SkipsGetParentLinkChanges()
    {
        var descriptor = BuildDescriptor();
        _sut.Bind(descriptor);
        StubWorkItemSubSteps();

        var target = new WorkspaceSyncTarget(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Project", "key", WorkItemSyncFilters.Empty);

        var result = await _sut.SyncWorkItems(target, SyncType.Full, Guid.CreateVersion7(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetParentLinkChanges(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SyncWorkItems_Full_SkipsTheTypeChangeQuery()
    {
        // Arrange
        _sut.Bind(BuildDescriptor());
        StubWorkItemSubSteps();
        var target = new WorkspaceSyncTarget(Guid.CreateVersion7(), Guid.CreateVersion7(), "Project", "key", WorkItemSyncFilters.Empty);

        // Act
        var result = await _sut.SyncWorkItems(target, SyncType.Full, Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetDeletedWorkItemIds(It.IsAny<AzureDevOpsConnectionContext>(), "Project", null, It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SyncWorkItems_Differential_QueriesTypeChangesSinceTheWatermark()
    {
        // Arrange
        _sut.Bind(BuildDescriptor());
        StubWorkItemSubSteps();
        var watermark = Instant.FromUtc(2026, 10, 1, 8, 30);
        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetWorkspaceMostRecentChangeDateQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<Instant?>(watermark));
        var target = new WorkspaceSyncTarget(Guid.CreateVersion7(), Guid.CreateVersion7(), "Project", "key", WorkItemSyncFilters.Empty);

        // Act
        var result = await _sut.SyncWorkItems(target, SyncType.Differential, Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetDeletedWorkItemIds(It.IsAny<AzureDevOpsConnectionContext>(), "Project", watermark.ToDateTimeUtc(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------- SyncWorkItems: partial-failure semantics --------

    [Fact]
    public async Task SyncWorkItems_SubStepFailure_ReturnsSuccessWithPartialFailureFlag()
    {
        var descriptor = BuildDescriptor();
        _sut.Bind(descriptor);
        StubWorkItemSubSteps();

        // Override only the deletes call to fail.
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetDeletedWorkItemIds(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<int[]>("AzDO returned 500"));

        var target = new WorkspaceSyncTarget(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Project", "key", WorkItemSyncFilters.Empty);

        var result = await _sut.SyncWorkItems(target, SyncType.Differential, Guid.CreateVersion7(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.HadPartialFailure.Should().BeTrue();
        result.Value.PartialFailureMessage.Should().Contain("deleted").And.Contain("AzDO returned 500");
    }

    [Fact]
    public async Task SyncWorkItems_AllSubStepsSucceed_ReturnsAggregatedCounters()
    {
        var descriptor = BuildDescriptor();
        _sut.Bind(descriptor);

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetWorkspaceWorkTypesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IWorkTypeDto>().AsReadOnly() as IReadOnlyList<IWorkTypeDto>));
        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetWorkspaceMostRecentChangeDateQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<Instant?>(null));

        var threeItems = new List<IExternalWorkItem>
        {
            Mock.Of<IExternalWorkItem>(), Mock.Of<IExternalWorkItem>(), Mock.Of<IExternalWorkItem>()
        };
        var twoParentChanges = new List<IExternalWorkItemLink>
        {
            Mock.Of<IExternalWorkItemLink>(), Mock.Of<IExternalWorkItemLink>()
        };
        var oneDepChange = new List<IExternalWorkItemLink> { Mock.Of<IExternalWorkItemLink>() };

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkItems(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string[]>(), It.IsAny<Dictionary<Guid, Guid?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(threeItems));
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetParentLinkChanges(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(twoParentChanges));
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetDependencyLinkChanges(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(oneDepChange));
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetDeletedWorkItemIds(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new[] { 1, 2, 3, 4 }));

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetIterationMappingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Guid>());
        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<SyncExternalWorkItemsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<SyncExternalWorkItemParentChangesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<SyncExternalWorkItemDependencyChangesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<DeleteExternalWorkItemsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(4));

        var target = new WorkspaceSyncTarget(
            Guid.CreateVersion7(), Guid.CreateVersion7(), "Project", "key", WorkItemSyncFilters.Empty);

        var result = await _sut.SyncWorkItems(target, SyncType.Differential, Guid.CreateVersion7(), CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.WorkItemsProcessed.Should().Be(3);
        result.Value.ParentLinkChangesProcessed.Should().Be(2);
        result.Value.DependencyLinkChangesProcessed.Should().Be(1);
        result.Value.DeletedWorkItemsProcessed.Should().Be(4);
        result.Value.HadPartialFailure.Should().BeFalse();
        result.Value.PartialFailureMessage.Should().BeNull();
    }

    /// <summary>
    /// Plans a connection with a synced project, a paused one and one not set up, and returns the
    /// set-up projects' Wayd workspace ids.
    /// </summary>
    private async Task<(Guid Synced, Guid Paused)> PlanThreeProjects(SyncableConnectionDescriptor? descriptor = null)
    {
        descriptor ??= BuildDescriptor();
        _sut.Bind(descriptor);
        var processId = Guid.CreateVersion7();
        var syncedId = Guid.CreateVersion7();
        var pausedId = Guid.CreateVersion7();
        var paused = Workspace(processId, internalId: pausedId, name: "Paused");
        paused.IntegrationState!.IsActive = false;
        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.Is<GetAzureDevOpsConnectionQuery>(q => q.ConnectionId == descriptor.ConnectionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildConnectionDetails(descriptor.ConnectionId,
                processes: [Process(externalId: processId)],
                workspaces: [Workspace(processId, internalId: syncedId, name: "Synced"), paused, Workspace(processId, name: "Not set up", isActive: false)]));
        (await _sut.GetSyncPlan(TestContext.Current.CancellationToken)).IsSuccess.Should().BeTrue();
        return (syncedId, pausedId);
    }

    private void StubHeldWorkItems(params int[] externalIds) =>
        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetExternalWorkItemIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(externalIds));

    private void StubChangedOutside(params int[] externalIds) =>
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetIdsOfWorkItemsChangedOutsideProjects(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<string[]>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(externalIds));

    private void StubDelete(Result<int> result) =>
        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<DeleteExternalWorkItemsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(result);

    private static readonly Instant _changedSince = Instant.FromUtc(2026, 10, 31, 9, 0);

    [Fact]
    public async Task SyncWorkItems_DeletesFromEverySetUpWorkspace()
    {
        // Arrange — a deleted item may still sit in the workspace of a project it left, paused or not
        var (syncedId, pausedId) = await PlanThreeProjects();
        StubWorkItemSubSteps();
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetDeletedWorkItemIds(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<string>(), It.IsAny<DateTime?>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new[] { 5 }));
        StubDelete(Result.Success(1));
        var target = new WorkspaceSyncTarget(Guid.CreateVersion7(), syncedId, "Synced", "key", WorkItemSyncFilters.Empty);

        // Act
        var result = await _sut.SyncWorkItems(target, SyncType.Differential, Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.DeletedWorkItemsProcessed.Should().Be(1);
        _mocker.GetMock<IDispatcher>()
            .Verify(s => s.Send(It.Is<DeleteExternalWorkItemsCommand>(c =>
                c.WorkspaceIds.Count == 2 && c.WorkspaceIds.Contains(syncedId) && c.WorkspaceIds.Contains(pausedId)
                && c.WorkItemIds.SequenceEqual(new[] { 5 })), It.IsAny<CancellationToken>()), Times.Once);
    }

    // -------- RemoveWorkItemsMovedOut --------

    [Fact]
    public async Task RemoveWorkItemsMovedOut_WithAWindow_DeletesTheHeldItemsChangedOutsideEverySetUpProject()
    {
        // Arrange — item 8 changed outside the set-up projects but Wayd never held it
        var (syncedId, pausedId) = await PlanThreeProjects();
        StubHeldWorkItems(7, 9, 100);
        StubChangedOutside(7, 8, 9);
        StubDelete(Result.Success(2));

        // Act
        var result = await _sut.RemoveWorkItemsMovedOut(_changedSince, Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().Be(2);
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetIdsOfWorkItemsChangedOutsideProjects(It.IsAny<AzureDevOpsConnectionContext>(),
                It.Is<string[]>(n => n.Length == 2 && n.Contains("Synced") && n.Contains("Paused")),
                _changedSince.ToDateTimeUtc(), It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<IDispatcher>()
            .Verify(s => s.Send(It.Is<DeleteExternalWorkItemsCommand>(c =>
                c.WorkspaceIds.Count == 2 && c.WorkspaceIds.Contains(syncedId) && c.WorkspaceIds.Contains(pausedId)
                && c.WorkItemIds.SequenceEqual(new[] { 7, 9 })), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RemoveWorkItemsMovedOut_CountsProjectsAnotherConnectionToTheOrganizationSetsUpAsSynced()
    {
        // Arrange — an item that moved to a project the other connection syncs is moved by that sync
        var descriptor = BuildDescriptor(systemId: "org-1");
        await PlanThreeProjects(descriptor);
        var siblingId = Guid.CreateVersion7();
        var unrelatedId = Guid.CreateVersion7();
        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetConnectionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ConnectionListDto> { ConnectionListItem(siblingId, "ORG-1"), ConnectionListItem(unrelatedId, "org-2") }.AsReadOnly());
        var processId = Guid.CreateVersion7();
        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.Is<GetAzureDevOpsConnectionQuery>(q => q.ConnectionId == siblingId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildConnectionDetails(siblingId, [Process(externalId: processId)],
                [Workspace(processId, name: "Elsewhere"), Workspace(processId, name: "Unused", isActive: false)], systemId: "org-1"));
        StubHeldWorkItems(7);
        StubChangedOutside();

        // Act
        var result = await _sut.RemoveWorkItemsMovedOut(_changedSince, Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetIdsOfWorkItemsChangedOutsideProjects(It.IsAny<AzureDevOpsConnectionContext>(),
                It.Is<string[]>(n => n.Length == 3 && n.Contains("Synced") && n.Contains("Paused") && n.Contains("Elsewhere")),
                It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<IDispatcher>()
            .Verify(s => s.Send(It.Is<GetAzureDevOpsConnectionQuery>(q => q.ConnectionId == unrelatedId), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveWorkItemsMovedOut_WithoutAWindow_LooksUpTheProjectOfEveryHeldItem()
    {
        // Arrange
        await PlanThreeProjects();
        StubHeldWorkItems(1, 2, 3);
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetIdsOfWorkItemsOutsideProjects(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<int[]>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new[] { 2 }));
        StubDelete(Result.Success(1));

        // Act
        var result = await _sut.RemoveWorkItemsMovedOut(null, Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be(1);
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetIdsOfWorkItemsOutsideProjects(It.IsAny<AzureDevOpsConnectionContext>(), new[] { 1, 2, 3 },
                It.Is<string[]>(n => n.Length == 2), It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetIdsOfWorkItemsChangedOutsideProjects(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<string[]>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveWorkItemsMovedOut_WhenNoHeldItemMovedOut_DeletesNothing()
    {
        // Arrange
        await PlanThreeProjects();
        StubHeldWorkItems(1);
        StubChangedOutside(8);

        // Act
        var result = await _sut.RemoveWorkItemsMovedOut(_changedSince, Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be(0);
        _mocker.GetMock<IDispatcher>()
            .Verify(s => s.Send(It.IsAny<DeleteExternalWorkItemsCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveWorkItemsMovedOut_WhenWaydHoldsNothing_CallsNoAzureDevOps()
    {
        // Arrange
        await PlanThreeProjects();
        StubHeldWorkItems();

        // Act
        var result = await _sut.RemoveWorkItemsMovedOut(null, Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be(0);
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetIdsOfWorkItemsOutsideProjects(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<int[]>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveWorkItemsMovedOut_WithNoSetUpProjects_ReturnsZero()
    {
        // Arrange
        var descriptor = BuildDescriptor();
        _sut.Bind(descriptor);
        var processId = Guid.CreateVersion7();
        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetAzureDevOpsConnectionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildConnectionDetails(descriptor.ConnectionId, [Process(externalId: processId)], [Workspace(processId, isActive: false)]));
        await _sut.GetSyncPlan(TestContext.Current.CancellationToken);

        // Act
        var result = await _sut.RemoveWorkItemsMovedOut(null, Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().Be(0);
        _mocker.GetMock<IDispatcher>()
            .Verify(s => s.Send(It.IsAny<GetExternalWorkItemIdsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RemoveWorkItemsMovedOut_WhenAzureDevOpsFails_ReturnsTheFailure()
    {
        // Arrange
        await PlanThreeProjects();
        StubHeldWorkItems(1);
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetIdsOfWorkItemsChangedOutsideProjects(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<string[]>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<int[]>("AzDO returned 500"));

        // Act
        var result = await _sut.RemoveWorkItemsMovedOut(_changedSince, Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("AzDO returned 500");
    }

    [Fact]
    public async Task RemoveWorkItemsMovedOut_WhenTheHeldItemsCannotBeRead_ReturnsTheFailure()
    {
        // Arrange
        await PlanThreeProjects();
        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetExternalWorkItemIdsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<int[]>("database unavailable"));

        // Act
        var result = await _sut.RemoveWorkItemsMovedOut(_changedSince, Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("database unavailable");
    }

    [Fact]
    public async Task RemoveWorkItemsMovedOut_WhenTheDeleteFails_ReturnsTheFailure()
    {
        // Arrange
        await PlanThreeProjects();
        StubHeldWorkItems(7);
        StubChangedOutside(7);
        StubDelete(Result.Failure<int>("delete failed"));

        // Act
        var result = await _sut.RemoveWorkItemsMovedOut(_changedSince, Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("delete failed");
    }

    [Fact]
    public async Task RemoveWorkItemsMovedOut_BeforeTheSyncPlan_Fails()
    {
        // Arrange
        _sut.Bind(BuildDescriptor());

        // Act
        var result = await _sut.RemoveWorkItemsMovedOut(null, Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
    }

    private static ConnectionListDto ConnectionListItem(Guid id, string systemId) =>
        new()
        {
            Id = id,
            Name = "Other Connection",
            SystemId = systemId,
            Connector = new SimpleNavigationDto { Id = (int)Connector.AzureDevOps, Name = "Azure DevOps" },
            Capabilities = [ConnectorCapabilityDto.FromEnum(ConnectorCapability.WorkItems)],
            IsActive = true,
            IsValidConfiguration = true,
            CanSync = true,
        };

    // -------- GetWorkItemHistory --------

    [Fact]
    public async Task GetWorkItemHistory_ReadsRevisionsOfSyncedTypesFromTheWatermark()
    {
        // Arrange
        _sut.Bind(BuildDescriptor());
        var target = new WorkspaceSyncTarget(Guid.CreateVersion7(), Guid.CreateVersion7(), "Project", "key", WorkItemSyncFilters.Empty);
        StubWorkspaceWorkTypes("User Story", "Bug");
        var revisions = new List<IExternalWorkItemRevision> { Mock.Of<IExternalWorkItemRevision>() };
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkItemRevisions(It.IsAny<AzureDevOpsConnectionContext>(), "Project", "watermark-1", new[] { "User Story", "Bug" }, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new AzureDevOpsWorkItemRevisionsBatch(revisions, "watermark-2", false)));

        // Act
        var result = await _sut.GetWorkItemHistory(target, "watermark-1", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Revisions.Should().BeSameAs(revisions);
        result.Value.NextWatermark.Should().Be("watermark-2");
        result.Value.IsLastBatch.Should().BeFalse();
    }

    [Fact]
    public async Task GetWorkItemHistory_WithNoContinuationToken_KeepsTheWatermark()
    {
        // Arrange
        _sut.Bind(BuildDescriptor());
        var target = new WorkspaceSyncTarget(Guid.CreateVersion7(), Guid.CreateVersion7(), "Project", "key", WorkItemSyncFilters.Empty);
        StubWorkspaceWorkTypes("User Story");
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkItemRevisions(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new AzureDevOpsWorkItemRevisionsBatch([], null, true)));

        // Act
        var result = await _sut.GetWorkItemHistory(target, "watermark-1", TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.NextWatermark.Should().Be("watermark-1");
        result.Value.IsLastBatch.Should().BeTrue();
    }

    [Fact]
    public async Task GetWorkItemHistory_WhenAzureDevOpsFails_ReturnsFailure()
    {
        // Arrange
        _sut.Bind(BuildDescriptor());
        var target = new WorkspaceSyncTarget(Guid.CreateVersion7(), Guid.CreateVersion7(), "Project", "key", WorkItemSyncFilters.Empty);
        StubWorkspaceWorkTypes("User Story");
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkItemRevisions(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<AzureDevOpsWorkItemRevisionsBatch>("AzDO returned 500"));

        // Act
        var result = await _sut.GetWorkItemHistory(target, null, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("AzDO returned 500");
    }

    [Fact]
    public async Task GetWorkItemHistory_ReadsTheWorkspaceWorkTypesOncePerBind()
    {
        // Arrange
        _sut.Bind(BuildDescriptor());
        var target = new WorkspaceSyncTarget(Guid.CreateVersion7(), Guid.CreateVersion7(), "Project", "key", WorkItemSyncFilters.Empty);
        StubWorkspaceWorkTypes("User Story");
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkItemRevisions(It.IsAny<AzureDevOpsConnectionContext>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new AzureDevOpsWorkItemRevisionsBatch([], "watermark-2", false)));

        // Act
        await _sut.GetWorkItemHistory(target, null, TestContext.Current.CancellationToken);
        await _sut.GetWorkItemHistory(target, "watermark-2", TestContext.Current.CancellationToken);

        // Assert
        _mocker.GetMock<IDispatcher>()
            .Verify(s => s.Send(It.IsAny<GetWorkspaceWorkTypesQuery>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetAllRevisions_ReadsTheRevisionsOfTheGivenItems()
    {
        // Arrange
        _sut.Bind(BuildDescriptor());
        var target = new WorkspaceSyncTarget(Guid.CreateVersion7(), Guid.CreateVersion7(), "Project", "key", WorkItemSyncFilters.Empty);
        var revisions = new List<IExternalWorkItemRevision> { Mock.Of<IExternalWorkItemRevision>() };
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetRevisionsOfWorkItems(It.IsAny<AzureDevOpsConnectionContext>(), It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 7, 9 })), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(revisions));

        // Act
        var result = await _sut.GetAllRevisions(target, [7, 9], TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(revisions);
    }

    private void StubWorkspaceWorkTypes(params string[] names)
    {
        var workTypes = names
            .Select(name => Mock.Of<IWorkTypeDto>(t => t.Name == name))
            .ToList();

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetWorkspaceWorkTypesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workTypes.AsReadOnly() as IReadOnlyList<IWorkTypeDto>));
    }

    // -------- RefreshOrganizationConfiguration delegates to init manager --------

    [Fact]
    public async Task RefreshOrganizationConfiguration_DelegatesToInitManager()
    {
        var descriptor = BuildDescriptor();
        _sut.Bind(descriptor);
        var syncId = Guid.CreateVersion7();

        _mocker.GetMock<IAzureDevOpsInitManager>()
            .Setup(m => m.SyncOrganizationConfiguration(descriptor.ConnectionId, It.IsAny<CancellationToken>(), syncId))
            .ReturnsAsync(Result.Success())
            .Verifiable();

        var result = await _sut.RefreshOrganizationConfiguration(syncId, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _mocker.GetMock<IAzureDevOpsInitManager>().Verify();
    }

    [Fact]
    public void SyncMethods_RequireBind()
    {
        // No Bind() call.
        Action act = () => _sut.TestConnection(CancellationToken.None);
        act.Should().Throw<InvalidOperationException>().WithMessage("*Bind*");
    }
}
