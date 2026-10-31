using FluentAssertions;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Queries.AzureDevOps;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Queries;

public class GetAzureDevOpsConnectionQueryHandlerTests
{
    private static readonly Instant _now = Instant.FromUtc(2026, 10, 31, 12, 0, 0);

    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly GetAzureDevOpsConnectionQueryHandler _sut;

    public GetAzureDevOpsConnectionQueryHandlerTests()
    {
        MapsterTestConfiguration.Ensure();
        _sut = new GetAzureDevOpsConnectionQueryHandler(_db, new FixedBaseUrlResolver());
    }

    [Fact]
    public async Task Handle_BuildsTheOrganizationUrlFromTheConfiguredBaseUrl()
    {
        // Arrange
        var connection = AzureDevOpsBoardsConnection.Create(
            "AzDO", null, "system-id", new AzureDevOpsBoardsConnectionConfiguration("contoso", "pat"), true, null, EventActor.System, _now);
        _db.AddAzureDevOpsBoardsConnection(connection);

        // Act
        var dto = await _sut.Handle(new GetAzureDevOpsConnectionQuery(connection.Id), TestContext.Current.CancellationToken);

        // Assert
        dto.Should().NotBeNull();
        dto!.Configuration.OrganizationUrl.Should().Be("https://devops.example.com/contoso");
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenTheConnectionDoesNotExist()
    {
        // Arrange
        var unknownId = Guid.CreateVersion7();

        // Act
        var dto = await _sut.Handle(new GetAzureDevOpsConnectionQuery(unknownId), TestContext.Current.CancellationToken);

        // Assert
        dto.Should().BeNull();
    }

    private sealed class FixedBaseUrlResolver : IAzureDevOpsUrlResolver
    {
        public string GetOrganizationUrl(string organization) => $"https://devops.example.com/{organization}";
    }
}
