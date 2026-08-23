using FluentAssertions;
using NodaTime;
using Wayd.AppIntegration.Application.Connections;
using Wayd.AppIntegration.Application.Connections.Dtos;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureDevOps;
using Wayd.AppIntegration.Application.Connections.Dtos.AzureOpenAI;
using Wayd.AppIntegration.Application.Connections.Dtos.Entra;
using Wayd.AppIntegration.Application.Connections.Dtos.Workday;
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

    [Fact]
    public async Task Handle_MasksEveryConnectorSecret()
    {
        // Arrange
        _db.AddConnections(CreateOneOfEachConcreteConnectionType());

        // Act
        var dtos = await Task.WhenAll(_db.Connections
            .Select(c => _sut.Handle(new GetConnectionQuery(c.Id), TestContext.Current.CancellationToken)));

        // Assert
        SecretsOf(dtos).Should().OnlyContain(s => s == ConnectionSecret.Mask,
            "masking belongs to the projection, so no caller can leak a secret by forgetting to mask");
    }

    [Fact]
    public async Task Handle_MaskDoesNotRevealTheSecretsLength()
    {
        // Arrange
        var shortSecret = "ab";
        var longSecret = new string('x', 120);
        var withShort = AzureDevOpsBoardsConnection.Create(
            "Short", null, null, new AzureDevOpsBoardsConnectionConfiguration("org-short", shortSecret), true, null, _now);
        var withLong = AzureDevOpsBoardsConnection.Create(
            "Long", null, null, new AzureDevOpsBoardsConnectionConfiguration("org-long", longSecret), true, null, _now);
        _db.AddConnections([withShort, withLong]);

        // Act
        var shortDto = (AzureDevOpsConnectionDetailsDto)(await _sut.Handle(
            new GetConnectionQuery(withShort.Id), TestContext.Current.CancellationToken))!;
        var longDto = (AzureDevOpsConnectionDetailsDto)(await _sut.Handle(
            new GetConnectionQuery(withLong.Id), TestContext.Current.CancellationToken))!;

        // Assert
        shortDto.Configuration.PersonalAccessToken.Should()
            .Be(longDto.Configuration.PersonalAccessToken,
                "a length-preserving mask discloses the credential's length to any Connections.View holder");
        longDto.Configuration.PersonalAccessToken.Should().NotContain(longSecret[..4]);
    }

    private static IEnumerable<string> SecretsOf(IEnumerable<ConnectionDetailsDto?> dtos) =>
        dtos.Select(dto => dto switch
        {
            AzureDevOpsConnectionDetailsDto azdo => azdo.Configuration.PersonalAccessToken,
            AzureOpenAIConnectionDetailsDto aoai => aoai.Configuration.ApiKey,
            EntraConnectionDetailsDto entra => entra.Configuration.ClientSecret,
            WorkdayConnectionDetailsDto workday => workday.Configuration.IsuPassword,
            _ => throw new InvalidOperationException($"No secret accessor for '{dto?.GetType().Name}'."),
        });

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
