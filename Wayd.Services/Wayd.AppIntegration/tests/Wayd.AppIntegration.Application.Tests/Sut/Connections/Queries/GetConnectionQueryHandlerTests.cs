using FluentAssertions;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Dtos;
using Wayd.AppIntegration.Application.Connections.Queries;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.AppIntegration.Domain.Models.Workday;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Queries;

public class GetConnectionQueryHandlerTests
{
    private static readonly Instant _now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);

    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly GetConnectionQueryHandler _sut;

    public GetConnectionQueryHandlerTests()
    {
        MapsterTestConfiguration.Ensure();
        _sut = new GetConnectionQueryHandler(_db);
    }

    [Fact]
    public async Task Handle_MapsEveryConcreteConnectionTypeToADerivedDto()
    {
        // Arrange
        var connections = CreateOneOfEachConcreteConnectionType();

        // Guard: every concrete Connection type in the domain assembly must be represented here.
        // When a new connector ships, this fails until the new type is added to
        // CreateOneOfEachConcreteConnectionType — and the handler's mapping switch throws for it
        // until that switch gets an arm, so a missing DTO mapping can never ship silently.
        var concreteTypes = typeof(Connection).Assembly.GetTypes()
            .Where(t => !t.IsAbstract && typeof(Connection).IsAssignableFrom(t));
        connections.Select(c => c.GetType()).Should().BeEquivalentTo(
            concreteTypes,
            "every concrete Connection type must be covered by this test — add the new connection type here and a mapping arm in GetConnectionQueryHandler");

        _db.AddConnections(connections);

        foreach (var connection in connections)
        {
            // Act
            var dto = await _sut.Handle(new GetConnectionQuery(connection.Id), TestContext.Current.CancellationToken);

            // Assert
            dto.Should().NotBeNull();
            dto!.GetType().Should().NotBe(typeof(ConnectionDetailsDto),
                $"'{connection.GetType().Name}' must map to its own details DTO — the base DTO has no configuration or $type discriminator");
        }
    }

    [Fact]
    public async Task Handle_Throws_WhenConnectionTypeHasNoDtoMapping()
    {
        // Arrange
        var connection = new UnmappedConnection();
        _db.AddConnection(connection);

        // Act
        var act = () => _sut.Handle(new GetConnectionQuery(connection.Id), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*UnmappedConnection*");
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenConnectionDoesNotExist()
    {
        // Arrange
        var unknownId = Guid.CreateVersion7();

        // Act
        var dto = await _sut.Handle(new GetConnectionQuery(unknownId), TestContext.Current.CancellationToken);

        // Assert
        dto.Should().BeNull();
    }

    private static List<Connection> CreateOneOfEachConcreteConnectionType() =>
    [
        AzureDevOpsBoardsConnection.Create(
            "AzDO", null, "system-id", new AzureDevOpsBoardsConnectionConfiguration("org", "pat"), true, null, _now),
        AzureOpenAIConnection.Create(
            "Azure OpenAI", null, new AzureOpenAIConnectionConfiguration("key", "model", "https://ai.acme.example"), true, _now),
        EntraConnection.Create(
            "Entra", null, new EntraConnectionConfiguration("tenant-id", "client-id", "client-secret"), true, _now),
        WorkdayConnection.Create(
            "Workday", null, new WorkdayConnectionConfiguration("https://wd.acme.example/ccx/service/acme_corp/Staffing/v46.1?wsdl", "isu-user", "isu-pass"), true, _now),
    ];

    private sealed class UnmappedConnection : Connection
    {
        public override bool HasActiveIntegrationObjects => false;
    }
}
