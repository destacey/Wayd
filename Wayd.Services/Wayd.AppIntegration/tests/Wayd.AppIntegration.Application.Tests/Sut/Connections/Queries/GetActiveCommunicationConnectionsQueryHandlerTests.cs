using FluentAssertions;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Queries;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.AppIntegration.Domain.Models.Teams;
using Wayd.Common.Application.Requests.AppIntegration;
using Wayd.Common.Domain.Enums.AppIntegrations;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.AppIntegration;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Queries;

public sealed class GetActiveCommunicationConnectionsQueryHandlerTests
{
    private static readonly Instant Now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);

    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly GetActiveCommunicationConnectionsQueryHandler _sut;

    public GetActiveCommunicationConnectionsQueryHandlerTests()
    {
        _sut = new GetActiveCommunicationConnectionsQueryHandler(_db);
    }

    [Fact]
    public async Task Handle_ReturnsOnlyActiveCommunicationConnections()
    {
        // Arrange
        var activeOne = TeamsConnection.Create(
            "Teams Prod",
            null,
            new TeamsConnectionConfiguration("t1", "c1", "secret"),
            true,
            EventActor.System,
            Now);

        var activeTwo = TeamsConnection.Create(
            "Teams DR",
            null,
            new TeamsConnectionConfiguration("t2", "c2", "secret"),
            true,
            EventActor.System,
            Now);

        var inactive = TeamsConnection.Create(
            "Teams Old",
            null,
            new TeamsConnectionConfiguration("t3", "c3", "secret"),
            true,
            EventActor.System,
            Now);
        inactive.Deactivate(ConnectionActivatableArgs.Create(EventActor.System, Now));

        var unverified = TeamsConnection.Create(
            "Teams Draft",
            null,
            new TeamsConnectionConfiguration("t4", "c4", "secret"),
            false,
            EventActor.System,
            Now);

        var entra = EntraConnection.Create(
            "Entra",
            null,
            new EntraConnectionConfiguration("tenant", "client", "secret"),
            true,
            EventActor.System,
            Now);

        _db.AddConnections([activeOne, activeTwo, inactive, unverified, entra]);

        // Act
        var result = await _sut.Handle(new GetActiveCommunicationConnectionsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(2);
        result.Select(r => r.Name).Should().BeEquivalentTo(["Teams Prod", "Teams DR"]);
        result.Should().OnlyContain(r => r.Connector == Connector.Teams && r.IsActive && r.IsValidConfiguration);
    }
}
