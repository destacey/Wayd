using CSharpFunctionalExtensions;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using Wayd.AppIntegration.Application.Connections.Dtos;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Managers;
using Wayd.AppIntegration.Application.Connections.Queries;
using Wayd.AppIntegration.Application.Connectors.Dtos;
using Wayd.AppIntegration.Application.Persistence;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Domain.Events;
using Wayd.Common.Application.Dtos;
using Wayd.Common.Application.Enums;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Interfaces.ExternalWork;
using Wayd.Common.Application.Requests.WorkManagement.Commands;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Models;
using Wayd.Integrations.Abstractions;
using Wayd.Tests.Shared;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Managers;

/// <summary>
/// Protocol tests for <see cref="WorkSyncRunner"/>. These assert the runner's contract with any
/// <see cref="IWorkItemSource"/> implementation — the source itself is fully mocked. The AzDO
/// entity is used only because <c>BuildDescriptor</c> currently knows how to construct a
/// descriptor from <c>AzureDevOpsBoardsConnection</c>; the runner protocol assertions are
/// connector-agnostic.
/// </summary>
public class WorkSyncRunnerTests
{
    private readonly AutoMocker _mocker;
    private readonly FakeAppIntegrationDbContext _db;
    private readonly TestingDateTimeProvider _clock;
    private readonly Mock<IWorkItemSource> _source;
    private readonly WorkSyncRunner _sut;

    public WorkSyncRunnerTests()
    {
        _mocker = new AutoMocker();
        _mocker.Use<ILogger<WorkSyncRunner>>(Mock.Of<ILogger<WorkSyncRunner>>());

        _db = new FakeAppIntegrationDbContext();
        _mocker.Use<IAppIntegrationDbContext>(_db);

        _clock = new TestingDateTimeProvider(new DateTime(2026, 05, 24, 10, 0, 0, DateTimeKind.Utc));
        _mocker.Use<IDateTimeProvider>(_clock);

        _source = new Mock<IWorkItemSource>(MockBehavior.Strict);
        _source.SetupGet(s => s.Connector).Returns(Connector.AzureDevOps);
        _source.Setup(s => s.Bind(It.IsAny<SyncableConnectionDescriptor>()))
            .Returns(Result.Success());

        _mocker.GetMock<IWorkItemSourceFactory>()
            .Setup(f => f.Create(It.IsAny<SyncableConnectionDescriptor>()))
            .Returns(Result.Success(_source.Object));

        // Exercise the real AzDO descriptor builder against the fake DbContext — same path
        // production takes.
        var descriptorBuilders = new ISyncableConnectionDescriptorBuilder[]
        {
            new AzureDevOpsConnectionDescriptorBuilder(_db)
        };
        _mocker.Use<IEnumerable<ISyncableConnectionDescriptorBuilder>>(descriptorBuilders);

        _sut = _mocker.CreateInstance<WorkSyncRunner>();
    }

    #region Test helpers

    private AzureDevOpsBoardsConnection SeedActiveAzdoConnection(string systemId = "system-1")
    {
        var processExternalId = Guid.CreateVersion7();
        var processInternalId = Guid.CreateVersion7();
        var workspaceExternalId = Guid.CreateVersion7();
        var workspaceInternalId = Guid.CreateVersion7();

        // Seed work process + workspace via the constructor (which accepts pre-built collections),
        // then activate the integration state so HasActiveIntegrationObjects is true.
        var config = new AzureDevOpsBoardsConnectionConfiguration(
            "TestOrg", "test-pat",
            workspaces: [AzureDevOpsBoardsWorkspace.Create(workspaceExternalId, "Project", "desc", processExternalId)],
            processes: [AzureDevOpsBoardsWorkProcess.Create(processExternalId, "Agile", "desc")]);

        var connection = AzureDevOpsBoardsConnection.Create(
            "Test Connection", "desc", systemId, config, configurationIsValid: true,
            teamConfiguration: null, EventActor.System, timestamp: _clock.Now);

        connection.UpdateWorkProcessIntegrationState(
            new IntegrationRegistration<Guid, Guid>(processExternalId, IntegrationState<Guid>.Create(processInternalId, true)),
            _clock.Now);
        connection.UpdateWorkspaceIntegrationState(
            new IntegrationRegistration<Guid, Guid>(workspaceExternalId, IntegrationState<Guid>.Create(workspaceInternalId, true)),
            _clock.Now);

        _db.AddAzureDevOpsBoardsConnection(connection);
        return connection;
    }

    private void SetupConnectionsQuery(params AzureDevOpsBoardsConnection[] entities)
    {
        var dtos = entities.Select(e => new ConnectionListDto
        {
            Id = e.Id,
            Name = e.Name,
            SystemId = e.SystemId,
            Connector = new SimpleNavigationDto { Id = (int)e.Connector, Name = "Azure DevOps" },
            Capabilities = [ConnectorCapabilityDto.FromEnum(ConnectorCapability.WorkItems)],
            IsActive = e.IsActive,
            IsValidConfiguration = e.IsValidConfiguration,
            CanSync = e.CanSync
        }).ToList().AsReadOnly() as IReadOnlyList<ConnectionListDto>;

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetConnectionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(dtos);
    }

    private void StubHappyPathSource(int workspaceCount = 1, int workItemsPerWorkspace = 5)
    {
        var targets = Enumerable.Range(0, workspaceCount)
            .Select(_ => new WorkspaceSyncTarget(
                ExternalWorkspaceId: Guid.CreateVersion7(),
                InternalWorkspaceId: Guid.CreateVersion7(),
                WorkspaceName: "Workspace",
                WorkspaceKey: "key",
                Filters: WorkItemSyncFilters.Empty))
            .ToList();

        _source.Setup(s => s.RefreshOrganizationConfiguration(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _source.Setup(s => s.GetSyncPlan(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<WorkspaceSyncTarget>>(targets));
        _source.Setup(s => s.PrepareWorkspaceForItemSync(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _source.Setup(s => s.SyncIterations(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _source.Setup(s => s.SyncWorkItems(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<SyncType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new WorkspaceItemsSyncResult(workItemsPerWorkspace, 0, 0, 0)));
        _source.Setup(s => s.GetWorkItemHistory(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WorkspaceSyncTarget _, string? watermark, CancellationToken _) => Result.Success(WorkItemHistoryBatch.Empty(watermark)));

        var dispatcher = _mocker.GetMock<IDispatcher>();
        dispatcher
            .Setup(s => s.Send(It.IsAny<ProcessDependenciesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        dispatcher
            .Setup(s => s.Send(It.IsAny<GetWorkItemHistoryWatermarkQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<string?>(null));
        dispatcher
            .Setup(s => s.Send(It.IsAny<SyncExternalWorkItemHistoryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(0));
        dispatcher
            .Setup(s => s.Send(It.IsAny<GetWorkItemsMissingRevisionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<int>>([]));
    }

    private static Result<WorkItemHistoryBatch> HistoryBatch(int revisionCount, string? nextWatermark, bool isLastBatch) =>
        Result.Success(new WorkItemHistoryBatch(
            [.. Enumerable.Range(1, revisionCount).Select(_ => Mock.Of<IExternalWorkItemRevision>())],
            nextWatermark,
            isLastBatch));

    #endregion

    [Fact]
    public async Task Run_WithNoActiveConnections_ReturnsSuccess_AndWritesNoSyncRun()
    {
        // No-op runs are success — scheduled "run all" jobs fire whether or not there's anything
        // to do, and returning failure would trip Hangfire's AutomaticRetry for no good reason.
        SetupConnectionsQuery(); // empty

        var result = await _sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _db.SyncRuns.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_HappyPath_PersistsSyncRunAsSucceeded()
    {
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource(workspaceCount: 2, workItemsPerWorkspace: 7);

        var result = await _sut.Run(SyncType.Full, SyncTriggerSource.Manual, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var runs = _db.SyncRuns.ToList();
        runs.Should().HaveCount(1);
        var run = runs.Single();
        run.Status.Should().Be(SyncRunStatus.Succeeded);
        run.ConnectionId.Should().Be(connection.Id);
        run.ConnectorType.Should().Be(Connector.AzureDevOps);
        run.SyncType.Should().Be(SyncType.Full);
        run.TriggerSource.Should().Be(SyncTriggerSource.Manual);
        run.WorkspacesPlanned.Should().Be(2);
        run.WorkspacesSucceeded.Should().Be(2);
        run.WorkspacesFailed.Should().Be(0);
        run.WorkItemsProcessed.Should().Be(14);
        run.StartedAt.Should().Be(_clock.Now);
        run.FinishedAt.Should().Be(_clock.Now);
        run.ErrorMessage.Should().BeNull();
        run.DetailsJson.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Run_HappyPath_WritesDetailsWithTheCamelCaseKeysTheClientReads()
    {
        // Arrange
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource(workItemsPerWorkspace: 7);

        // Act
        await _sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, CancellationToken.None);

        // Assert
        var details = _db.SyncRuns.Single().DetailsJson;
        details.Should().Contain("\"internalWorkspaceId\":");
        details.Should().Contain("\"workItemsProcessed\":7");
        details.Should().NotContain("\"WorkItemsProcessed\"");
    }

    [Fact]
    public async Task Run_HappyPath_CallsSourceInExpectedOrder()
    {
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource(workspaceCount: 1);

        var sequence = new List<string>();
        _source.Setup(s => s.RefreshOrganizationConfiguration(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add(nameof(IWorkItemSource.RefreshOrganizationConfiguration)))
            .ReturnsAsync(Result.Success());
        _source.Setup(s => s.GetSyncPlan(It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add(nameof(IWorkItemSource.GetSyncPlan)))
            .ReturnsAsync(Result.Success<IReadOnlyList<WorkspaceSyncTarget>>([new WorkspaceSyncTarget(
                Guid.CreateVersion7(), Guid.CreateVersion7(), "w", "k", WorkItemSyncFilters.Empty)]));
        _source.Setup(s => s.PrepareWorkspaceForItemSync(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add(nameof(IWorkItemSource.PrepareWorkspaceForItemSync)))
            .ReturnsAsync(Result.Success());
        _source.Setup(s => s.SyncIterations(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add(nameof(IWorkItemSource.SyncIterations)))
            .ReturnsAsync(Result.Success());
        _source.Setup(s => s.SyncWorkItems(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<SyncType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add(nameof(IWorkItemSource.SyncWorkItems)))
            .ReturnsAsync(Result.Success(WorkspaceItemsSyncResult.Zero));
        _source.Setup(s => s.GetWorkItemHistory(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add(nameof(IWorkItemSource.GetWorkItemHistory)))
            .ReturnsAsync(Result.Success(WorkItemHistoryBatch.Empty(null)));

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<ProcessDependenciesCommand>(), It.IsAny<CancellationToken>()))
            .Callback(() => sequence.Add(nameof(ProcessDependenciesCommand)))
            .ReturnsAsync(Result.Success());

        await _sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, CancellationToken.None);

        sequence.Should().Equal(
            nameof(IWorkItemSource.RefreshOrganizationConfiguration),
            nameof(IWorkItemSource.GetSyncPlan),
            nameof(IWorkItemSource.PrepareWorkspaceForItemSync),
            nameof(IWorkItemSource.SyncIterations),
            nameof(IWorkItemSource.SyncWorkItems),
            nameof(IWorkItemSource.GetWorkItemHistory),
            nameof(ProcessDependenciesCommand));
    }

    [Fact]
    public async Task Run_Differential_PullsHistoryFromTheStoredWatermarkUntilTheLastBatch()
    {
        // Arrange
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource();
        var dispatcher = _mocker.GetMock<IDispatcher>();
        dispatcher
            .Setup(s => s.Send(It.IsAny<GetWorkItemHistoryWatermarkQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<string?>("w0"));
        _source.Setup(s => s.GetWorkItemHistory(It.IsAny<WorkspaceSyncTarget>(), "w0", It.IsAny<CancellationToken>()))
            .ReturnsAsync(HistoryBatch(3, "w1", isLastBatch: false));
        _source.Setup(s => s.GetWorkItemHistory(It.IsAny<WorkspaceSyncTarget>(), "w1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(HistoryBatch(2, "w2", isLastBatch: true));
        var applied = new List<SyncExternalWorkItemHistoryCommand>();
        dispatcher
            .Setup(s => s.Send(It.IsAny<SyncExternalWorkItemHistoryCommand>(), It.IsAny<CancellationToken>()))
            .Callback((ICommand<int> command, CancellationToken _) => applied.Add((SyncExternalWorkItemHistoryCommand)command))
            .ReturnsAsync(() => Result.Success(applied.Count * 2));

        // Act
        await _sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, CancellationToken.None);

        // Assert
        applied.Select(c => (c.ConnectionId, c.Revisions.Count, c.Watermark)).Should().Equal(
            (connection.Id, 3, "w1"),
            (connection.Id, 2, "w2"));
        var details = _db.SyncRuns.Single().DetailsJson;
        details.Should().Contain("\"workItemRevisionsProcessed\":5");
        details.Should().Contain("\"workItemHistoryPeriodsWritten\":6");
    }

    [Fact]
    public async Task Run_ItemsMissingRevisions_AreFilledFromTheItemsWithoutMovingTheWatermark()
    {
        // Arrange
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource();
        _source.Setup(s => s.GetWorkItemHistory(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HistoryBatch(2, "w1", isLastBatch: true));
        var dispatcher = _mocker.GetMock<IDispatcher>();
        dispatcher
            .Setup(s => s.Send(It.IsAny<GetWorkItemsMissingRevisionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<int>>([7, 9]));
        _source.Setup(s => s.GetAllRevisions(It.IsAny<WorkspaceSyncTarget>(), It.Is<IReadOnlyCollection<int>>(ids => ids.SequenceEqual(new[] { 7, 9 })), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<IExternalWorkItemRevision>>(
                [.. Enumerable.Range(1, 4).Select(_ => Mock.Of<IExternalWorkItemRevision>())]));
        var applied = new List<SyncExternalWorkItemHistoryCommand>();
        dispatcher
            .Setup(s => s.Send(It.IsAny<SyncExternalWorkItemHistoryCommand>(), It.IsAny<CancellationToken>()))
            .Callback((ICommand<int> command, CancellationToken _) => applied.Add((SyncExternalWorkItemHistoryCommand)command))
            .ReturnsAsync(Result.Success(1));

        // Act
        await _sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, CancellationToken.None);

        // Assert
        applied.Select(c => (c.Revisions.Count, c.Watermark, c.FilledWorkItemIds?.ToArray())).Should().BeEquivalentTo(
            new (int, string?, int[]?)[] { (2, "w1", null), (4, null, [7, 9]) },
            o => o.WithStrictOrdering());
        var details = _db.SyncRuns.Single().DetailsJson;
        details.Should().Contain("\"workItemRevisionsProcessed\":6");
        details.Should().Contain("\"workItemHistoryPeriodsWritten\":2");
    }

    [Fact]
    public async Task Run_FillsMissingRevisionsOnlyAfterEveryWorkspacesStream()
    {
        // Arrange — a revision made in the second workspace must arrive through its stream, not be
        // fetched from the item by the first workspace's fill
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource(workspaceCount: 2);
        var calls = new List<string>();
        _source.Setup(s => s.GetWorkItemHistory(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("stream"))
            .ReturnsAsync(HistoryBatch(0, "w1", isLastBatch: true));
        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<GetWorkItemsMissingRevisionsQuery>(), It.IsAny<CancellationToken>()))
            .Callback(() => calls.Add("fill"))
            .ReturnsAsync(Result.Success<IReadOnlyList<int>>([]));

        // Act
        await _sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, CancellationToken.None);

        // Assert
        calls.Should().Equal("stream", "stream", "fill", "fill");
    }

    [Fact]
    public async Task Run_FillFailsPartWay_KeepsWhatEarlierBatchesAppliedAndReportsTheError()
    {
        // Arrange — two batches of items to fill; the second cannot be read
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource();
        _source.Setup(s => s.GetWorkItemHistory(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HistoryBatch(2, "w1", isLastBatch: true));
        var dispatcher = _mocker.GetMock<IDispatcher>();
        dispatcher
            .Setup(s => s.Send(It.IsAny<GetWorkItemsMissingRevisionsQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<int>>([.. Enumerable.Range(1, 60)]));
        _source.SetupSequence(s => s.GetAllRevisions(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success<IReadOnlyList<IExternalWorkItemRevision>>([.. Enumerable.Range(1, 3).Select(_ => Mock.Of<IExternalWorkItemRevision>())]))
            .ReturnsAsync(Result.Failure<IReadOnlyList<IExternalWorkItemRevision>>("Revisions unavailable"));
        dispatcher
            .Setup(s => s.Send(It.IsAny<SyncExternalWorkItemHistoryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(1));

        // Act
        await _sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, CancellationToken.None);

        // Assert
        var run = _db.SyncRuns.Single();
        run.Status.Should().Be(SyncRunStatus.Succeeded);
        run.ErrorsCount.Should().Be(1);
        run.DetailsJson.Should().Contain("\"workItemRevisionsProcessed\":5");
        run.DetailsJson.Should().Contain("\"workItemHistoryPeriodsWritten\":2");
        run.DetailsJson.Should().Contain("\"hadPartialFailure\":true");
        run.DetailsJson.Should().Contain("Filling missing work item revisions: Revisions unavailable");
    }

    [Fact]
    public async Task Run_NoItemsMissingRevisions_FetchesNothingFromTheItems()
    {
        // Arrange
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource();

        // Act
        await _sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, CancellationToken.None);

        // Assert
        _source.Verify(s => s.GetAllRevisions(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<IReadOnlyCollection<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_HistoryFailsPartWay_RecordsWhatWasAppliedBeforeTheFailure()
    {
        // Arrange
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource();
        _source.Setup(s => s.GetWorkItemHistory(It.IsAny<WorkspaceSyncTarget>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(HistoryBatch(3, "w1", isLastBatch: false));
        _source.Setup(s => s.GetWorkItemHistory(It.IsAny<WorkspaceSyncTarget>(), "w1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<WorkItemHistoryBatch>("Revisions unavailable"));
        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<SyncExternalWorkItemHistoryCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(2));

        // Act
        await _sut.Run(SyncType.Full, SyncTriggerSource.Manual, CancellationToken.None);

        // Assert
        var details = _db.SyncRuns.Single().DetailsJson;
        details.Should().Contain("\"workItemRevisionsProcessed\":3");
        details.Should().Contain("\"workItemHistoryPeriodsWritten\":2");
        details.Should().Contain("Work item history: Revisions unavailable");
    }

    [Fact]
    public async Task Run_Full_ReplaysHistoryFromTheStartIgnoringTheStoredWatermark()
    {
        // Arrange — the stored history is kept; the replay's already-applied revisions are skipped
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource();
        var watermarks = new List<string?>();
        _source.Setup(s => s.GetWorkItemHistory(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .Callback((WorkspaceSyncTarget _, string? watermark, CancellationToken _) => watermarks.Add(watermark))
            .ReturnsAsync(HistoryBatch(1, "w1", isLastBatch: true));

        // Act
        await _sut.Run(SyncType.Full, SyncTriggerSource.Manual, CancellationToken.None);

        // Assert
        watermarks.Should().Equal([null]);
        _mocker.GetMock<IDispatcher>()
            .Verify(s => s.Send(It.IsAny<GetWorkItemHistoryWatermarkQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_HistoryFails_KeepsTheWorkspaceSucceededButDegraded()
    {
        // Arrange
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource(workItemsPerWorkspace: 4);
        _source.Setup(s => s.GetWorkItemHistory(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<WorkItemHistoryBatch>("Revisions unavailable"));

        // Act
        var result = await _sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, CancellationToken.None);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var run = _db.SyncRuns.Single();
        run.Status.Should().Be(SyncRunStatus.Succeeded);
        run.WorkspacesSucceeded.Should().Be(1);
        run.WorkItemsProcessed.Should().Be(4);
        run.ErrorsCount.Should().Be(1);
        run.DetailsJson.Should().Contain("Work item history: Revisions unavailable");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)] // revisions are not progress: a repeated batch is skipped as already applied
    public async Task Run_HistorySourceThatDoesNotAdvance_StopsAndRecordsTheFailure(int revisionCount)
    {
        // Arrange
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource();
        _source.Setup(s => s.GetWorkItemHistory(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(HistoryBatch(revisionCount, null, isLastBatch: false));

        // Act
        await _sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, CancellationToken.None);

        // Assert
        _source.Verify(s => s.GetWorkItemHistory(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<string?>(), It.IsAny<CancellationToken>()), Times.Once);
        _db.SyncRuns.Single().DetailsJson.Should().Contain("without advancing its watermark");
    }

    [Fact]
    public async Task Run_SourceFactoryFailure_SkipsConnectionAndReturnsSuccess()
    {
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        _mocker.GetMock<IWorkItemSourceFactory>()
            .Setup(f => f.Create(It.IsAny<SyncableConnectionDescriptor>()))
            .Returns(Result.Failure<IWorkItemSource>("No source registered."));

        var result = await _sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, CancellationToken.None);

        // Skipping is not a top-level failure
        result.IsSuccess.Should().BeTrue();
        _db.SyncRuns.Should().BeEmpty(); // no run is started when the source can't be resolved
        _source.Verify(s => s.RefreshOrganizationConfiguration(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_RefreshOrganizationConfigurationFails_MarksRunAsFailed()
    {
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource();
        _source.Setup(s => s.RefreshOrganizationConfiguration(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("Unable to reach AzDO"));

        var result = await _sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, CancellationToken.None);

        result.IsSuccess.Should().BeTrue(); // per-connection failure is not a job-level failure
        var run = _db.SyncRuns.Single();
        run.Status.Should().Be(SyncRunStatus.Failed);
        run.ErrorMessage.Should().Contain("Unable to reach AzDO");
        run.FinishedAt.Should().NotBeNull();
        _source.Verify(s => s.GetSyncPlan(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_GetSyncPlanFails_MarksRunAsFailed()
    {
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource();
        _source.Setup(s => s.GetSyncPlan(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure<IReadOnlyList<WorkspaceSyncTarget>>("Plan unavailable"));

        var result = await _sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var run = _db.SyncRuns.Single();
        run.Status.Should().Be(SyncRunStatus.Failed);
        run.ErrorMessage.Should().Contain("Plan unavailable");
        _source.Verify(s => s.PrepareWorkspaceForItemSync(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_WorkspacePrepFails_ContinuesToOtherWorkspacesAndRecordsFailure()
    {
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource(workspaceCount: 2);

        // First call fails, second succeeds.
        _source.SetupSequence(s => s.PrepareWorkspaceForItemSync(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("Schema refresh failed"))
            .ReturnsAsync(Result.Success());

        var result = await _sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var run = _db.SyncRuns.Single();
        run.Status.Should().Be(SyncRunStatus.Succeeded);
        run.WorkspacesPlanned.Should().Be(2);
        run.WorkspacesSucceeded.Should().Be(1);
        run.WorkspacesFailed.Should().Be(1);
        // Iterations/items should still be called for the second (succeeded) workspace.
        _source.Verify(s => s.SyncIterations(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
        _source.Verify(s => s.SyncWorkItems(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<SyncType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_MultipleConnections_OneFailingDoesNotStopTheOther()
    {
        var connectionA = SeedActiveAzdoConnection(systemId: "system-a");
        var connectionB = SeedActiveAzdoConnection(systemId: "system-b");
        SetupConnectionsQuery(connectionA, connectionB);
        StubHappyPathSource();

        // First connection's GetSyncPlan throws; second runs normally.
        _source.SetupSequence(s => s.RefreshOrganizationConfiguration(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"))
            .ReturnsAsync(Result.Success());

        var result = await _sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var runs = _db.SyncRuns.ToList();
        runs.Should().HaveCount(2);
        runs.Should().Contain(r => r.Status == SyncRunStatus.Failed && r.ErrorMessage == "boom");
        runs.Should().Contain(r => r.Status == SyncRunStatus.Succeeded);
    }

    [Fact]
    public async Task Run_Cancelled_MarksRunAsCancelledAndRethrows()
    {
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource();

        using var cts = new CancellationTokenSource();
        _source.Setup(s => s.RefreshOrganizationConfiguration(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ThrowsAsync(new OperationCanceledException());

        var act = async () => await _sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
        var run = _db.SyncRuns.Single();
        run.Status.Should().Be(SyncRunStatus.Cancelled);
        run.FinishedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Run_ProcessDependenciesFailure_StillMarksRunSucceededButRecordsError()
    {
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource();
        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.IsAny<ProcessDependenciesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Failure("Dependencies failed"));

        var result = await _sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var run = _db.SyncRuns.Single();
        run.Status.Should().Be(SyncRunStatus.Succeeded);
        run.ErrorsCount.Should().Be(1);
    }

    [Fact]
    public async Task Run_PartialWorkspaceFailureInsideSource_PreservesSuccessButCountsAccurately()
    {
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource();

        // SyncWorkItems returns success with HadPartialFailure=true (e.g. parent-link sub-step failed).
        _source.Setup(s => s.SyncWorkItems(It.IsAny<WorkspaceSyncTarget>(), It.IsAny<SyncType>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new WorkspaceItemsSyncResult(
                WorkItemsProcessed: 3,
                ParentLinkChangesProcessed: 0,
                DependencyLinkChangesProcessed: 0,
                DeletedWorkItemsProcessed: 0,
                HadPartialFailure: true,
                PartialFailureMessage: "deps sub-step failed")));

        var result = await _sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        var run = _db.SyncRuns.Single();
        run.Status.Should().Be(SyncRunStatus.Succeeded);
        // Workspace counted as success because the source returned Result.Success
        run.WorkspacesSucceeded.Should().Be(1);
        run.WorkItemsProcessed.Should().Be(3);
        run.DetailsJson.Should().Contain("deps sub-step failed");
        // Partial failure must bump ErrorsCount so dashboards can flag degraded runs without
        // parsing DetailsJson.
        run.ErrorsCount.Should().Be(1);
    }

    [Theory]
    [InlineData(SyncTriggerSource.Manual)]
    [InlineData(SyncTriggerSource.Scheduled)]
    [InlineData(SyncTriggerSource.Api)]
    public async Task Run_PersistsTriggerSourceOnSyncRun(SyncTriggerSource trigger)
    {
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource();

        await _sut.Run(SyncType.Differential, trigger, CancellationToken.None);

        _db.SyncRuns.Single().TriggerSource.Should().Be(trigger);
    }

    [Fact]
    public async Task Run_NoDescriptorBuilderRegistered_SkipsConnectionAndStartsNoSyncRun()
    {
        // Re-create the SUT with an EMPTY descriptor-builder set, simulating a connector that
        // has neither a builder nor an IWorkItemSource registered. The runner must skip the
        // connection rather than throw or persist a half-baked SyncRun.
        _mocker.Use<IEnumerable<ISyncableConnectionDescriptorBuilder>>(Array.Empty<ISyncableConnectionDescriptorBuilder>());
        var sut = _mocker.CreateInstance<WorkSyncRunner>();

        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource();

        var result = await sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        _db.SyncRuns.Should().BeEmpty();
        _source.Verify(s => s.RefreshOrganizationConfiguration(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_QueriesConnectionsFilteredByWorkSyncCategory()
    {
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource();

        await _sut.Run(SyncType.Differential, SyncTriggerSource.Scheduled, CancellationToken.None);

        // Critical invariant: the runner must only see WorkSync connectors. If this assertion
        // is changed to allow unfiltered queries, future People/AI-sync connectors will be
        // pulled into the work-sync pipeline.
        _mocker.GetMock<IDispatcher>().Verify(s => s.Send(
            It.Is<GetConnectionsQuery>(q =>
                q.IncludeInactive == false
                && q.Capability == ConnectorCapability.WorkItems),
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Run_BindCalledOncePerConnection()
    {
        var connection = SeedActiveAzdoConnection();
        SetupConnectionsQuery(connection);
        StubHappyPathSource();

        await _sut.Run(SyncType.Full, SyncTriggerSource.Scheduled, CancellationToken.None);

        _mocker.GetMock<IWorkItemSourceFactory>()
            .Verify(f => f.Create(It.Is<SyncableConnectionDescriptor>(d =>
                d.ConnectionId == connection.Id
                && d.Connector == Connector.AzureDevOps
                && d.SystemId == connection.SystemId)),
                Times.Once);
    }

    #region Per-connection Run(connectionId, syncType, trigger, ct)

    private void SetupSingleConnectionQuery(Guid connectionId, AzureDevOpsBoardsConnection? connection)
    {
        AzureDevOpsConnectionDetailsDto? details = connection is null ? null : new AzureDevOpsConnectionDetailsDto
        {
            Id = connection.Id,
            Name = connection.Name,
            Connector = new SimpleNavigationDto { Id = (int)connection.Connector, Name = "Azure DevOps" },
            Capabilities = [ConnectorCapabilityDto.FromEnum(ConnectorCapability.WorkItems)],
            IsActive = connection.IsActive,
            IsValidConfiguration = connection.IsValidConfiguration,
            SystemId = connection.SystemId,
            Configuration = new AzureDevOpsConnectionConfigurationDto
            {
                Organization = "TestOrg",
                PersonalAccessToken = "test-pat",
                OrganizationUrl = "https://dev.azure.com/TestOrg",
                WorkProcesses = [],
                Workspaces = [],
            },
            TeamConfiguration = new AzureDevOpsTeamConfigurationDto
            {
                WorkspaceTeams = [],
            },
        };

        _mocker.GetMock<IDispatcher>()
            .Setup(s => s.Send(It.Is<GetConnectionQuery>(q => q.Id == connectionId), It.IsAny<CancellationToken>()))
            .ReturnsAsync(details);
    }

    [Fact]
    public async Task Run_PerConnection_HappyPath_PersistsSyncRunForThatConnectionOnly()
    {
        var connection = SeedActiveAzdoConnection();
        SetupSingleConnectionQuery(connection.Id, connection);
        StubHappyPathSource(workspaceCount: 1, workItemsPerWorkspace: 4);

        var result = await _sut.Run(connection.Id, SyncType.Differential, SyncTriggerSource.Manual, CancellationToken.None);

        result.IsSuccess.Should().BeTrue();

        var runs = _db.SyncRuns.ToList();
        runs.Should().HaveCount(1);
        var run = runs.Single();
        run.ConnectionId.Should().Be(connection.Id);
        run.ConnectorType.Should().Be(Connector.AzureDevOps);
        run.SyncType.Should().Be(SyncType.Differential);
        run.TriggerSource.Should().Be(SyncTriggerSource.Manual);
        run.Status.Should().Be(SyncRunStatus.Succeeded);
        run.WorkspacesPlanned.Should().Be(1);
        run.WorkspacesSucceeded.Should().Be(1);
        run.WorkItemsProcessed.Should().Be(4);

        // Org-wide query must not be used by the per-connection overload.
        _mocker.GetMock<IDispatcher>()
            .Verify(s => s.Send(It.IsAny<GetConnectionsQuery>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Run_PerConnection_ConnectionNotFound_ReturnsFailureAndWritesNoSyncRun()
    {
        var connectionId = Guid.CreateVersion7();
        SetupSingleConnectionQuery(connectionId, null);

        var result = await _sut.Run(connectionId, SyncType.Differential, SyncTriggerSource.Manual, CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(connectionId.ToString());
        _db.SyncRuns.Should().BeEmpty();

        // Source factory must not be invoked when the connection lookup failed.
        _mocker.GetMock<IWorkItemSourceFactory>()
            .Verify(f => f.Create(It.IsAny<SyncableConnectionDescriptor>()), Times.Never);
    }

    [Fact]
    public async Task Run_PerConnection_Cancelled_MarksRunAsCancelledAndRethrows()
    {
        var connection = SeedActiveAzdoConnection();
        SetupSingleConnectionQuery(connection.Id, connection);
        StubHappyPathSource();

        using var cts = new CancellationTokenSource();
        // Cancel before the first ThrowIfCancellationRequested checkpoint inside RunConnection.
        _source.Setup(s => s.RefreshOrganizationConfiguration(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .Callback(() => cts.Cancel())
            .ReturnsAsync(Result.Success());

        var act = async () => await _sut.Run(connection.Id, SyncType.Full, SyncTriggerSource.Manual, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();

        var runs = _db.SyncRuns.ToList();
        runs.Should().HaveCount(1);
        runs.Single().Status.Should().Be(SyncRunStatus.Cancelled);
    }

    #endregion
}
