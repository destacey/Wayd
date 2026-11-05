using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events.Planning.Risks;
using Wayd.Planning.Application.Risks.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Planning.Application.Tests.Sut.Risks.Commands;

public sealed class DeleteRiskCommandHandlerTests : IDisposable
{
    private static readonly Instant Now = Instant.FromUtc(2026, 3, 2, 14, 0);

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<ICurrentUser> _currentUser = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(new FakeClock(Now));
    private readonly DeleteRiskCommandHandler _handler;

    public DeleteRiskCommandHandlerTests()
    {
        _handler = new DeleteRiskCommandHandler(_dbContext, _currentUser.Object, _dateTimeProvider, Mock.Of<ILogger<DeleteRiskCommandHandler>>());
    }

    [Fact]
    public async Task Handle_RiskExists_RemovesItAndRaisesDeleted()
    {
        // Arrange
        var risk = new RiskFaker().Generate();
        risk.ClearDomainEvents();
        var otherRisk = new RiskFaker().Generate();
        _dbContext.AddRisks([risk, otherRisk]);

        // Act
        var result = await _handler.Handle(new DeleteRiskCommand(risk.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _dbContext.Risks.Should().NotContain(risk).And.Contain(otherRisk);
        var deleted = risk.DomainEvents.Should().ContainSingle().Which.Should().BeOfType<RiskDeletedEvent>().Subject;
        deleted.Id.Should().Be(risk.Id);
        deleted.Timestamp.Should().Be(Now);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_RiskDoesNotExist_FailsWithoutSaving()
    {
        // Arrange
        _dbContext.AddRisks([new RiskFaker().Generate()]);

        // Act
        var result = await _handler.Handle(new DeleteRiskCommand(Guid.CreateVersion7()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Risk not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
