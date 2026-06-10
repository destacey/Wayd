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

public class GetConnectionsQueryHandlerTests
{
    private static readonly Instant _now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);

    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly GetConnectionsQueryHandler _sut;

    public GetConnectionsQueryHandlerTests()
    {
        MapsterTestConfiguration.Ensure();
        _sut = new GetConnectionsQueryHandler(_db);
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
            "every concrete Connection type must be covered by this test — add the new connection type here and a mapping arm in GetConnectionsQueryHandler");

        _db.AddConnections(connections);

        // Act
        var result = await _sut.Handle(new GetConnectionsQuery(IncludeInactive: true), TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(connections.Count);
        result.Should().AllSatisfy(dto =>
            dto.GetType().Should().NotBe(typeof(ConnectionListDto),
                "the base DTO has no configuration or $type discriminator — every connection must map to its derived DTO"));
    }

    [Fact]
    public async Task Handle_Throws_WhenConnectionTypeHasNoDtoMapping()
    {
        // Arrange
        _db.AddConnection(new UnmappedConnection());

        // Act
        var act = () => _sut.Handle(new GetConnectionsQuery(IncludeInactive: true), TestContext.Current.CancellationToken);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*UnmappedConnection*");
    }

    [Fact]
    public async Task Handle_ExcludesInactiveConnections_ByDefault()
    {
        // Arrange
        var active = AzureDevOpsBoardsConnection.Create(
            "Active", null, "system-id", new AzureDevOpsBoardsConnectionConfiguration("org", "pat"), true, null, _now);
        var inactive = AzureDevOpsBoardsConnection.Create(
            "Inactive", null, "system-id-2", new AzureDevOpsBoardsConnectionConfiguration("org2", "pat2"), true, null, _now);
        inactive.Deactivate(_now);
        _db.AddConnections([active, inactive]);

        // Act
        var result = await _sut.Handle(new GetConnectionsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Active");
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
