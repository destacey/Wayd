using FluentAssertions;
using Moq;
using NodaTime;
using Wayd.AppIntegration.Application.Connections.Queries;
using Wayd.AppIntegration.Application.Tests.Infrastructure;
using Wayd.AppIntegration.Domain.Models.Entra;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Events;

namespace Wayd.AppIntegration.Application.Tests.Sut.Connections.Queries;

public sealed class GetConnectionActivitiesQueryHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 6, 1, 12, 0, 0);

    private readonly FakeAppIntegrationDbContext _db = new();
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private readonly GetConnectionActivitiesQueryHandler _sut;

    public GetConnectionActivitiesQueryHandlerTests()
    {
        _sut = new GetConnectionActivitiesQueryHandler(_db, _activityLogReader.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenConnectionDoesNotExist()
    {
        // Act
        var result = await _sut.Handle(new GetConnectionActivitiesQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _activityLogReader.Verify(r => r.Read(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenConnectionIsDeleted()
    {
        // Arrange
        var connection = CreateConnection();
        connection.Delete(EventActor.System, Now);
        connection.IsDeleted = true;
        _db.AddConnection(connection);

        // Act
        var result = await _sut.Handle(new GetConnectionActivitiesQuery(connection.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ReadsTheConnectionsActivity()
    {
        // Arrange
        var connection = CreateConnection();
        _db.AddConnection(connection);

        var expected = new PagedResponse<ActivityLogDto>(
        [
            new ActivityLogDto
            {
                Id = 1,
                EventType = "ConnectionCredentialsChangedEvent",
                DomainArea = "AppIntegration",
                AggregateType = "Connection",
                AggregateId = connection.Id,
                ActorKind = EventActorKind.User,
                Timestamp = Now,
                Payload = "{}",
                Summary = "Connection Credentials Changed",
            },
        ], 1, 1, 50);
        _activityLogReader
            .Setup(r => r.Read(connection.Id, "Connection", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _sut.Handle(new GetConnectionActivitiesQuery(connection.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().BeEquivalentTo(expected);
    }

    private static EntraConnection CreateConnection() =>
        EntraConnection.Create("People", null, new EntraConnectionConfiguration("tenant-id", "client-id", "client-secret"), true, EventActor.System, Now);

    public void Dispose() => _db.Dispose();
}
