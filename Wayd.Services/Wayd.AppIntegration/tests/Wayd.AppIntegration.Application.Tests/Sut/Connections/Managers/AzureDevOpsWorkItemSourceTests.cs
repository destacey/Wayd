using CSharpFunctionalExtensions;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Managers;
using Wayd.AppIntegration.Application.Connections.Queries.AzureDevOps;
using Wayd.AppIntegration.Application.Interfaces;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Enums;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Requests.Planning.Iterations;
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
    private readonly AzureDevOpsWorkItemSource _sut;

    public AzureDevOpsWorkItemSourceTests()
    {
        _mocker = new AutoMocker();
        _mocker.Use<ILogger<AzureDevOpsWorkItemSource>>(Mock.Of<ILogger<AzureDevOpsWorkItemSource>>());
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
            Category = new SimpleNavigationDto { Id = (int)ConnectorCategory.WorkSync, Name = "Work Sync" },
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
            .Setup(s => s.GetWorkProcess(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(workProcessConfig.Object));

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<GetWorkProcessSchemesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<IWorkProcessSchemeDto>().AsReadOnly() as IReadOnlyList<IWorkProcessSchemeDto>);

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<UpdateExternalWorkProcessCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        // SyncWorkspace
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkspace(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new Mock<IExternalWorkspaceConfiguration>().Object));

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<UpdateExternalWorkspaceCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
    }

    private void StubWorkItemSubSteps()
    {
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<GetWorkspaceWorkTypesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IWorkTypeDto>().AsReadOnly() as IReadOnlyList<IWorkTypeDto>));

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<GetWorkspaceMostRecentChangeDateQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<Instant?>(null));

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetWorkItems(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string[]>(), It.IsAny<Dictionary<Guid, Guid?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IExternalWorkItem>()));

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetParentLinkChanges(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IExternalWorkItemLink>()));

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetDependencyLinkChanges(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IExternalWorkItemLink>()));

        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetDeletedWorkItemIds(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(Array.Empty<int>()));
    }

    #endregion

    // -------- Bind --------

    [Fact]
    public void Bind_RejectsWrongConnector()
    {
        var descriptor = new SyncableConnectionDescriptor(
            Guid.CreateVersion7(),
            Connector.OpenAI,
            SystemId: null,
            Configuration: new AzureDevOpsBoardsConnectionConfiguration("o", "p"),
            TeamConfiguration: null);

        var result = _sut.Bind(descriptor);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("OpenAI");
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

        _mocker.GetMock<ISender>()
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
    public async Task GetSyncPlan_SkipsWorkspacesWithInactiveProcess()
    {
        var descriptor = BuildDescriptor();
        _sut.Bind(descriptor);

        var inactiveProcessId = Guid.CreateVersion7();
        var details = BuildConnectionDetails(
            descriptor.ConnectionId,
            processes: [Process(externalId: inactiveProcessId, isActive: false)],
            workspaces: [Workspace(inactiveProcessId)]);

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<GetAzureDevOpsConnectionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var result = await _sut.GetSyncPlan(CancellationToken.None);

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

        _mocker.GetMock<ISender>()
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

        _mocker.GetMock<ISender>()
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

        _mocker.GetMock<ISender>()
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

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<GetAzureDevOpsConnectionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        var planResult = await _sut.GetSyncPlan(CancellationToken.None);
        planResult.Value.Should().HaveCount(2);

        StubWorkProcessSync();

        foreach (var target in planResult.Value)
        {
            var prepResult = await _sut.PrepareWorkspaceForItemSync(target, Guid.CreateVersion7(), CancellationToken.None);
            prepResult.IsSuccess.Should().BeTrue();
        }

        // Same process across both workspaces: GetWorkProcess should only fire once.
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetWorkProcess(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        // GetWorkspace is per-workspace.
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetWorkspace(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task PrepareWorkspaceForItemSync_ResetsDedupOnRebind()
    {
        var descriptor = BuildDescriptor();
        _sut.Bind(descriptor);

        var processId = Guid.CreateVersion7();
        var details = BuildConnectionDetails(
            descriptor.ConnectionId,
            processes: [Process(externalId: processId)],
            workspaces: [Workspace(processId)]);

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<GetAzureDevOpsConnectionQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);

        StubWorkProcessSync();

        var plan1 = await _sut.GetSyncPlan(CancellationToken.None);
        await _sut.PrepareWorkspaceForItemSync(plan1.Value.Single(), Guid.CreateVersion7(), CancellationToken.None);

        // Rebind for a fresh sync cycle.
        _sut.Bind(descriptor);
        var plan2 = await _sut.GetSyncPlan(CancellationToken.None);
        await _sut.PrepareWorkspaceForItemSync(plan2.Value.Single(), Guid.CreateVersion7(), CancellationToken.None);

        // Each Bind cycle should re-sync the work process once.
        _mocker.GetMock<IAzureDevOpsService>()
            .Verify(s => s.GetWorkProcess(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
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
            .Verify(s => s.GetParentLinkChanges(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Once);
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
            .Verify(s => s.GetParentLinkChanges(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()), Times.Never);
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
            .Setup(s => s.GetDeletedWorkItemIds(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
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

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<GetWorkspaceWorkTypesQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new List<IWorkTypeDto>().AsReadOnly() as IReadOnlyList<IWorkTypeDto>));
        _mocker.GetMock<ISender>()
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
            .Setup(s => s.GetWorkItems(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string[]>(), It.IsAny<Dictionary<Guid, Guid?>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(threeItems));
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetParentLinkChanges(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(twoParentChanges));
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetDependencyLinkChanges(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<string[]>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(oneDepChange));
        _mocker.GetMock<IAzureDevOpsService>()
            .Setup(s => s.GetDeletedWorkItemIds(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new[] { 1, 2, 3, 4 }));

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<GetIterationMappingsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<string, Guid>());
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<SyncExternalWorkItemsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<SyncExternalWorkItemParentChangesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<SyncExternalWorkItemDependencyChangesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<DeleteExternalWorkItemsCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

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
