using FluentAssertions;
using Moq.AutoMock;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Commands.Teams;
using Wayd.AppIntegration.Application.Persistence;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models.Teams;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.AppIntegration;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Commands.Teams;

public sealed class DeleteTeamsConnectionCommandHandlerTests
{
    private static readonly Instant _now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);

    private readonly AutoMocker _mocker = new();
    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly DeleteTeamsConnectionCommandHandler _sut;

    public DeleteTeamsConnectionCommandHandlerTests()
    {
        _mocker.Use<IAppIntegrationDbContext>(_db);
        _mocker.GetMock<IDateTimeProvider>().SetupGet(p => p.Now).Returns(_now);
        _mocker.GetMock<ICurrentUser>().Setup(u => u.GetUserId()).Returns("user-1");
        _sut = _mocker.CreateInstance<DeleteTeamsConnectionCommandHandler>();
    }

    [Fact]
    public async Task Handle_RemovesTheConnection()
    {
        // Arrange
        var connection = CreateConnection();

        // Act
        var result = await _sut.Handle(
            new DeleteTeamsConnectionCommand(connection.Id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _db.TeamsConnections.Should().BeEmpty();
        connection.DomainEvents.OfType<ConnectionDeletedEvent>().Should().ContainSingle();
        _db.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenConnectionMissing_ReturnsFailure()
    {
        // Arrange
        var id = Guid.NewGuid();

        // Act
        var result = await _sut.Handle(
            new DeleteTeamsConnectionCommand(id),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(id.ToString());
        result.Error.Should().Contain("not found");
        _db.SaveChangesCallCount.Should().Be(0);
    }

    private TeamsConnection CreateConnection()
    {
        var connection = TeamsConnection.Create(
            "Teams",
            null,
            new TeamsConnectionConfiguration("tenant-id", "client-id", "client-secret"),
            configurationIsValid: true,
            EventActor.System,
            _now);

        _db.AddTeamsConnection(connection);
        return connection;
    }
}
