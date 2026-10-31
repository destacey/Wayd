using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Commands.AzureDevOps;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Commands;

public class SetAzureDevOpsWorkspaceSyncCommandHandlerTests
{
    private static readonly Instant _now = Instant.FromUtc(2026, 10, 31, 12, 0, 0);

    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly SetAzureDevOpsWorkspaceSyncCommandHandler _sut;

    public SetAzureDevOpsWorkspaceSyncCommandHandlerTests()
    {
        _sut = new SetAzureDevOpsWorkspaceSyncCommandHandler(_db, NullLogger<SetAzureDevOpsWorkspaceSyncCommandHandler>.Instance);
    }

    [Fact]
    public async Task Handle_PausesASetUpProjectAndSaves()
    {
        // Arrange
        var workspaceExternalId = Guid.CreateVersion7();
        var connection = ConnectionWithWorkspace(workspaceExternalId, isSetUp: true);
        _db.AddAzureDevOpsBoardsConnection(connection);

        // Act
        var result = await _sut.Handle(new SetAzureDevOpsWorkspaceSyncCommand(connection.Id, workspaceExternalId, false), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        connection.Configuration.Workspaces.Single().IntegrationIsActive.Should().BeFalse();
        _db.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTheProjectIsNotSetUp_FailsWithoutSaving()
    {
        // Arrange
        var workspaceExternalId = Guid.CreateVersion7();
        var connection = ConnectionWithWorkspace(workspaceExternalId, isSetUp: false);
        _db.AddAzureDevOpsBoardsConnection(connection);

        // Act
        var result = await _sut.Handle(new SetAzureDevOpsWorkspaceSyncCommand(connection.Id, workspaceExternalId, true), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _db.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheConnectionDoesNotExist_FailsWithoutSaving()
    {
        // Arrange
        var unknownConnectionId = Guid.CreateVersion7();

        // Act
        var result = await _sut.Handle(new SetAzureDevOpsWorkspaceSyncCommand(unknownConnectionId, Guid.CreateVersion7(), true), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _db.SaveChangesCallCount.Should().Be(0);
    }

    private static AzureDevOpsBoardsConnection ConnectionWithWorkspace(Guid workspaceExternalId, bool isSetUp)
    {
        var workspace = AzureDevOpsBoardsWorkspace.Create(workspaceExternalId, "Project", null, Guid.CreateVersion7());
        if (isSetUp)
            workspace.AddIntegrationState(IntegrationState<Guid>.Create(Guid.CreateVersion7(), true));

        var config = new AzureDevOpsBoardsConnectionConfiguration("org", "pat", workspaces: [workspace]);
        return AzureDevOpsBoardsConnection.Create("AzDO", null, "system-id", config, true, null, EventActor.System, _now);
    }
}
