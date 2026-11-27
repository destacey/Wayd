using FluentAssertions;
using Wayd.Common.Application.Requests.WorkManagement.Queries;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Models;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Application.WorkProcesses.Queries;
using Wayd.Work.Domain.Models;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.WorkProcesses.Queries;

public sealed class GetIntegrationRegistrationsForWorkProcessesQueryHandlerTests : IDisposable
{
    private readonly FakeWorkDbContext _dbContext = new();
    private readonly GetIntegrationRegistrationsForWorkProcessesQueryHandler _handler;

    public GetIntegrationRegistrationsForWorkProcessesQueryHandlerTests()
    {
        _handler = new GetIntegrationRegistrationsForWorkProcessesQueryHandler(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    private WorkProcess AddProcess(OwnershipInfo ownershipInfo, bool isActive = true)
    {
        var process = new WorkProcessFaker().WithOwnershipInfo(ownershipInfo).WithIsActive(isActive).Generate();
        _dbContext.AddWorkProcess(process);
        return process;
    }

    private static OwnershipInfo ManagedAs(Guid externalProcessId) =>
        OwnershipInfo.CreateExternalOwned(Connector.AzureDevOps, null, WorkProcess.ToExternalId(externalProcessId));

    [Fact]
    public async Task Handle_ReturnsEveryManagedProcess_KeyedByItsExternalProcessId()
    {
        // Arrange
        var agileId = Guid.NewGuid();
        var scrumId = Guid.NewGuid();
        var agile = AddProcess(ManagedAs(agileId));
        var scrum = AddProcess(ManagedAs(scrumId), isActive: false);
        AddProcess(OwnershipInfo.CreateWaydOwned());

        // Act
        var result = await _handler.Handle(new GetIntegrationRegistrationsForWorkProcessesQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(r => (r.ExternalId, r.IntegrationState.InternalId, r.IntegrationState.IsActive)).Should().BeEquivalentTo(
        [
            (agileId, agile.Id, true),
            (scrumId, scrum.Id, false),
        ]);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyTheRequestedProcess_WhenAnExternalIdIsGiven()
    {
        // Arrange
        var agileId = Guid.NewGuid();
        var agile = AddProcess(ManagedAs(agileId));
        AddProcess(ManagedAs(Guid.NewGuid()));

        // Act
        var result = await _handler.Handle(new GetIntegrationRegistrationsForWorkProcessesQuery(agileId), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle();
        result[0].ExternalId.Should().Be(agileId);
        result[0].IntegrationState.InternalId.Should().Be(agile.Id);
    }

    [Fact]
    public async Task Handle_LeavesOutAProcessReleasedToWayd_ThoughItKeepsItsReference()
    {
        // Arrange
        var released = ManagedAs(Guid.NewGuid()).Release().Value;
        AddProcess(released);

        // Act
        var result = await _handler.Handle(new GetIntegrationRegistrationsForWorkProcessesQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_LeavesOutAManagedProcess_WhoseExternalIdIsNotAProcessId()
    {
        // Arrange
        var agileId = Guid.NewGuid();
        AddProcess(ManagedAs(agileId));
        AddProcess(OwnershipInfo.CreateExternalOwned(Connector.AzureDevOps, null, "not-a-process-id"));

        // Act
        var result = await _handler.Handle(new GetIntegrationRegistrationsForWorkProcessesQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(r => r.ExternalId).Should().Equal(agileId);
    }
}
