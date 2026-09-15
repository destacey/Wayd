using FluentAssertions;
using Moq;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Planning.Application.Risks.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.Risks.Queries;

public sealed class GetRiskActivitiesQueryHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<IActivityLogReader> _activityLogReader = new();
    private readonly GetRiskActivitiesQueryHandler _handler;

    public GetRiskActivitiesQueryHandlerTests()
    {
        _handler = new GetRiskActivitiesQueryHandler(_dbContext, _activityLogReader.Object);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_ReturnsNull_WhenRiskDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(
            new GetRiskActivitiesQuery(new IdOrKey(Guid.NewGuid())),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeNull();
        _activityLogReader.Verify(r => r.Read(It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ReadsTheRisksHistory_ByKey()
    {
        // Arrange
        var risk = new RiskFaker().WithKey(88).Generate();
        _dbContext.AddRisk(risk);

        var expected = new PagedResponse<ActivityLogDto>([], 0, 1, 50);
        _activityLogReader
            .Setup(r => r.Read(risk.Id, "Risk", 1, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        // Act
        var result = await _handler.Handle(
            new GetRiskActivitiesQuery(new IdOrKey("88")),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.Should().BeSameAs(expected);
    }
}
