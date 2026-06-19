using CSharpFunctionalExtensions;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.AutoMock;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Managers;
using Wayd.AppIntegration.Application.Persistence;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.AppIntegration.Domain.Models.Workday;
using Wayd.Common.Application.Employees.Commands;
using Wayd.Common.Application.Enums;
using Wayd.Common.Application.Identity.Users;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Integrations.Abstractions;
using Wayd.Tests.Shared;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Managers;

/// <summary>
/// Protocol tests for <see cref="PeopleSyncRunner"/>. These assert the runner's contract with any
/// <see cref="IEmployeeSource"/> implementation — the source itself is fully mocked. The Entra and
/// Workday entities are used only because the descriptor builders currently know how to construct
/// descriptors from them; the runner protocol assertions are connector-agnostic.
/// </summary>
public class PeopleSyncRunnerTests
{
    private readonly AutoMocker _mocker;
    private readonly FakeAppIntegrationDbContext _db;
    private readonly TestingDateTimeProvider _clock;
    private readonly Mock<IEmployeeSource> _source;
    private readonly PeopleSyncRunner _sut;

    public PeopleSyncRunnerTests()
    {
        _mocker = new AutoMocker();
        _mocker.Use<ILogger<PeopleSyncRunner>>(Mock.Of<ILogger<PeopleSyncRunner>>());

        _db = new FakeAppIntegrationDbContext();
        _mocker.Use<IAppIntegrationDbContext>(_db);

        _clock = new TestingDateTimeProvider(new DateTime(2026, 06, 10, 10, 0, 0, DateTimeKind.Utc));
        _mocker.Use<IDateTimeProvider>(_clock);

        _source = new Mock<IEmployeeSource>(MockBehavior.Strict);
        _source.SetupGet(s => s.SupportsIncremental).Returns(false);
        _source.SetupGet(s => s.MatchBy).Returns(EmployeeMatchProperty.Email);
        _source.Setup(s => s.GetEmployees(It.IsAny<Instant?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new EmployeeFetchResult([FakeEmployee()], [])));

        _mocker.GetMock<IEmployeeSourceFactory>()
            .Setup(f => f.Create(It.IsAny<SyncableConnectionDescriptor>()))
            .Returns(Result.Success(_source.Object));

        // Exercise the real descriptor builders against the fake DbContext — same path
        // production takes.
        var descriptorBuilders = new ISyncableConnectionDescriptorBuilder[]
        {
            new EntraConnectionDescriptorBuilder(_db),
            new WorkdayConnectionDescriptorBuilder(_db)
        };
        _mocker.Use<IEnumerable<ISyncableConnectionDescriptorBuilder>>(descriptorBuilders);

        _mocker.GetMock<ISender>()
            .Setup(s => s.Send(It.IsAny<BulkUpsertEmployeesCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _mocker.GetMock<IUserService>()
            .Setup(u => u.UpdateMissingEmployeeIds(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());
        _mocker.GetMock<IUserService>()
            .Setup(u => u.SyncUsersFromEmployeeRecords(It.IsAny<List<IExternalEmployee>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success());

        _sut = _mocker.CreateInstance<PeopleSyncRunner>();
    }

    #region Test helpers

    private static IExternalEmployee FakeEmployee() =>
        Mock.Of<IExternalEmployee>(e => e.EmployeeNumber == "E1");

    private EntraConnection SeedActiveEntraConnection()
    {
        var connection = EntraConnection.Create(
            "Entra", null,
            new EntraConnectionConfiguration("tenant-id", "client-id", "client-secret"),
            configurationIsValid: true, _clock.Now);

        // The runner gates on the base Connections set; the descriptor builder loads the typed set.
        _db.AddConnection(connection);
        _db.AddEntraConnection(connection);
        return connection;
    }

    private WorkdayConnection SeedActiveWorkdayConnection()
    {
        var connection = WorkdayConnection.Create(
            "Workday", null,
            new WorkdayConnectionConfiguration("https://wd.acme.example/ccx/service/acme_corp/Staffing/v46.1?wsdl", "isu-user", "isu-pass"),
            configurationIsValid: true, _clock.Now);

        _db.AddConnection(connection);
        _db.AddWorkdayConnection(connection);
        return connection;
    }

    #endregion

    [Fact]
    public async Task Run_ReturnsSuccessWithoutSyncRuns_WhenNoActiveConnectionsExist()
    {
        // Act
        var result = await _sut.Run(SyncTriggerSource.Scheduled, SyncType.Full, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _db.SyncRuns.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_Aborts_WhenMultipleActivePeopleConnectionsExist()
    {
        // Arrange
        SeedActiveEntraConnection();
        SeedActiveWorkdayConnection();

        // Act
        var result = await _sut.Run(SyncTriggerSource.Scheduled, SyncType.Full, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Multiple active PeopleSync connections");
        _db.SyncRuns.Should().BeEmpty();
    }

    [Fact]
    public async Task Run_ResolvesSourceThroughFactory_AndPersistsSucceededRun()
    {
        // Arrange
        var connection = SeedActiveEntraConnection();

        // Act
        var result = await _sut.Run(SyncTriggerSource.Scheduled, SyncType.Full, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mocker.GetMock<IEmployeeSourceFactory>().Verify(f => f.Create(It.Is<SyncableConnectionDescriptor>(d =>
            d.ConnectionId == connection.Id
            && d.Connector == Connector.Entra
            && d.Configuration is EntraConnectionConfiguration)), Times.Once);

        var run = _db.SyncRuns.Single();
        run.ConnectionId.Should().Be(connection.Id);
        run.Status.Should().Be(SyncRunStatus.Succeeded);
    }

    [Fact]
    public async Task Run_PassesSourceMatchByAndDeactivatesMissing_OnFullSync()
    {
        // Arrange
        SeedActiveEntraConnection();
        _source.SetupGet(s => s.MatchBy).Returns(EmployeeMatchProperty.EmployeeNumber);

        // Act
        var result = await _sut.Run(SyncTriggerSource.Manual, SyncType.Full, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _mocker.GetMock<ISender>().Verify(s => s.Send(
            It.Is<BulkUpsertEmployeesCommand>(c =>
                c.MatchBy == EmployeeMatchProperty.EmployeeNumber && c.DeactivateMissing),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Run_PassesWatermarkAndSkipsDeactivation_WhenSourceSupportsIncremental()
    {
        // Arrange
        var connection = SeedActiveWorkdayConnection();
        _source.SetupGet(s => s.SupportsIncremental).Returns(true);

        var priorRun = SyncRun.Start(connection.Id, Connector.Workday, SyncType.Full, SyncTriggerSource.Scheduled, _clock.Now.Minus(Duration.FromHours(2)));
        priorRun.MarkSucceeded(_clock.Now.Minus(Duration.FromHours(1)));
        _db.AddSyncRun(priorRun);

        // Act
        var result = await _sut.Run(SyncTriggerSource.Scheduled, SyncType.Differential, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _source.Verify(s => s.GetEmployees(priorRun.FinishedAt, It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<ISender>().Verify(s => s.Send(
            It.Is<BulkUpsertEmployeesCommand>(c => !c.DeactivateMissing),
            It.IsAny<CancellationToken>()), Times.Once);

        var run = _db.SyncRuns.Single(r => r.Id != priorRun.Id);
        run.SyncType.Should().Be(SyncType.Differential);
    }

    [Fact]
    public async Task Run_DegradesToFull_WhenSourceDoesNotSupportIncremental()
    {
        // Arrange — a prior successful run exists, but the source can't do deltas.
        var connection = SeedActiveEntraConnection();

        var priorRun = SyncRun.Start(connection.Id, Connector.Entra, SyncType.Full, SyncTriggerSource.Scheduled, _clock.Now.Minus(Duration.FromHours(2)));
        priorRun.MarkSucceeded(_clock.Now.Minus(Duration.FromHours(1)));
        _db.AddSyncRun(priorRun);

        // Act
        var result = await _sut.Run(SyncTriggerSource.Scheduled, SyncType.Differential, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _source.Verify(s => s.GetEmployees(It.Is<Instant?>(i => i == null), It.IsAny<CancellationToken>()), Times.Once);
        _mocker.GetMock<ISender>().Verify(s => s.Send(
            It.Is<BulkUpsertEmployeesCommand>(c => c.DeactivateMissing),
            It.IsAny<CancellationToken>()), Times.Once);

        var run = _db.SyncRuns.Single(r => r.Id != priorRun.Id);
        run.SyncType.Should().Be(SyncType.Full);
    }

    [Fact]
    public async Task RunForConnection_Fails_WhenConnectorLacksPeopleCapability()
    {
        // Arrange — an active work-sync connection
        var connection = AzureDevOpsBoardsConnection.Create(
            "AzDO", null, "system-id", new AzureDevOpsBoardsConnectionConfiguration("org", "pat"), true, null, _clock.Now);
        _db.AddConnection(connection);

        // Act
        var result = await _sut.Run(connection.Id, SyncTriggerSource.Manual, SyncType.Full, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("not a people-sync connection");
    }

    [Fact]
    public async Task RunForConnection_Fails_WhenConnectionIsInactive()
    {
        // Arrange
        var connection = SeedActiveEntraConnection();
        connection.Deactivate(_clock.Now);

        // Act
        var result = await _sut.Run(connection.Id, SyncTriggerSource.Manual, SyncType.Full, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("inactive");
        _db.SyncRuns.Should().BeEmpty();
    }

    [Fact]
    public async Task RunForConnection_PersistsFailedRun_WhenSourceResolutionFails()
    {
        // Arrange
        var connection = SeedActiveEntraConnection();
        _mocker.GetMock<IEmployeeSourceFactory>()
            .Setup(f => f.Create(It.IsAny<SyncableConnectionDescriptor>()))
            .Returns(Result.Failure<IEmployeeSource>("No IEmployeeSource is registered for connector 'Entra'."));

        // Act
        var result = await _sut.Run(connection.Id, SyncTriggerSource.Manual, SyncType.Full, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        var run = _db.SyncRuns.Single();
        run.Status.Should().Be(SyncRunStatus.Failed);
    }

    [Fact]
    public async Task Run_FailsRun_WhenFullFetchReturnsZeroEmployees()
    {
        // Arrange
        SeedActiveEntraConnection();
        _source.Setup(s => s.GetEmployees(It.IsAny<Instant?>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Result.Success(new EmployeeFetchResult([], [])));

        // Act — the outer run reports success (per-connection failures are recorded, not thrown)
        var result = await _sut.Run(SyncTriggerSource.Scheduled, SyncType.Full, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var run = _db.SyncRuns.Single();
        run.Status.Should().Be(SyncRunStatus.Failed);
    }
}
