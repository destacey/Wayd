using FluentAssertions;
using Moq.AutoMock;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Commands.Teams;
using Wayd.AppIntegration.Application.Persistence;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models.Teams;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Commands.Teams;

public sealed class UpdateTeamsConnectionCommandHandlerTests
{
    private const string StoredClientSecret = "stored-teams-secret-0001";
    private const string TenantId = "acme-tenant-id";
    private const string ClientId = "acme-client-id";

    private static readonly Instant _now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);

    private readonly AutoMocker _mocker = new();
    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly UpdateTeamsConnectionCommandHandler _sut;

    public UpdateTeamsConnectionCommandHandlerTests()
    {
        _mocker.Use<IAppIntegrationDbContext>(_db);
        _mocker.GetMock<IDateTimeProvider>().SetupGet(p => p.Now).Returns(_now);
        _mocker.GetMock<ICurrentUser>().Setup(u => u.GetUserId()).Returns("user-1");
        _sut = _mocker.CreateInstance<UpdateTeamsConnectionCommandHandler>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Handle_PreservesStoredSecret_WhenSecretIsBlank(string? submittedSecret)
    {
        // Arrange
        var connection = CreateConnection();
        var command = new UpdateTeamsConnectionCommand(
            connection.Id,
            "Renamed",
            "A new description",
            TenantId,
            ClientId,
            submittedSecret);

        // Act
        var result = await _sut.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        connection.Configuration.ClientSecret.Should().Be(StoredClientSecret);
        connection.Name.Should().Be("Renamed");
        connection.Description.Should().Be("A new description");
        _db.SaveChangesCallCount.Should().Be(1);
    }

    private TeamsConnection CreateConnection()
    {
        var connection = TeamsConnection.Create(
            "Teams",
            "Original description",
            new TeamsConnectionConfiguration(TenantId, ClientId, StoredClientSecret),
            configurationIsValid: true,
            EventActor.System,
            _now);

        _db.AddTeamsConnection(connection);
        return connection;
    }
}
