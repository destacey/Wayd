using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Identity.OidcProviders.Queries;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Events;
using Wayd.Tests.Shared.Data;

namespace Wayd.Common.Application.Tests.Sut.Identity.OidcProviders;

public sealed class GetOidcProviderActivitiesQueryHandlerTests : IDisposable
{
    private readonly FakeWaydDbContext _dbContext = new();
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private readonly GetOidcProviderActivitiesQueryHandler _handler;

    public GetOidcProviderActivitiesQueryHandlerTests()
    {
        _handler = new GetOidcProviderActivitiesQueryHandler(_dbContext, _activityLogReader.Object);
    }

    [Fact]
    public async Task Handle_ReturnsNull_WhenProviderDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(new GetOidcProviderActivitiesQuery(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _activityLogReader.Verify(r => r.Read(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReadsTheProvidersActivity()
    {
        // Arrange
        var provider = new OidcProviderFaker().Generate();
        _dbContext.OidcProviders.Add(provider);

        var expected = new PagedResponse<ActivityLogDto>(
        [
            new ActivityLogDto
            {
                Id = 1,
                EventType = "OidcProviderDisabledEvent",
                DomainArea = "Identity",
                AggregateType = "OidcProvider",
                AggregateId = provider.Id,
                ActorKind = EventActorKind.User,
                Timestamp = Instant.FromUnixTimeSeconds(100),
                Payload = "{}",
                Summary = "Oidc Provider Disabled",
            },
        ], 1, 2, 25);
        _activityLogReader
            .Setup(r => r.Read(provider.Id, "OidcProvider", 2, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _handler.Handle(new GetOidcProviderActivitiesQuery(provider.Id, 2, 25), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Should().BeEquivalentTo(expected);
    }

    public void Dispose() => _dbContext.Dispose();
}
