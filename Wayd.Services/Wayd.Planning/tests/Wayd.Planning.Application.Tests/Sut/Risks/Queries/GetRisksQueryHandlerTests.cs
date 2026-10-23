using FluentAssertions;
using Wayd.Planning.Application.Risks.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.Risks.Queries;

public sealed class GetRisksQueryHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly GetRisksQueryHandler _handler;

    public GetRisksQueryHandlerTests()
    {
        _handler = new GetRisksQueryHandler(_dbContext);
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_OrdersByKey()
    {
        // Arrange
        var teamId = Guid.NewGuid();
        _dbContext.AddRisks([
            new RiskFaker().WithKey(30).WithTeamId(teamId).Generate(),
            new RiskFaker().WithKey(10).WithTeamId(teamId).Generate(),
            new RiskFaker().WithKey(20).WithTeamId(teamId).Generate(),
        ]);

        // Act
        var result = await _handler.Handle(new GetRisksQuery(teamId, false), TestContext.Current.CancellationToken);

        // Assert
        result.Select(r => r.Key).Should().Equal(10, 20, 30);
    }
}
