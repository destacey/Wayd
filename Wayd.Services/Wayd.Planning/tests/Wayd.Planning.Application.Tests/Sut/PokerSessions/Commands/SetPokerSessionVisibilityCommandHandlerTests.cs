using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Commands;

public class SetPokerSessionVisibilityCommandHandlerTests : IDisposable
{
    private readonly FakePokerSessionAccess _access = new();
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<IPokerSessionNotifier> _mockNotifier = new();
    private readonly SetPokerSessionVisibilityCommandHandler _handler;

    public SetPokerSessionVisibilityCommandHandlerTests()
    {
        _handler = new SetPokerSessionVisibilityCommandHandler(_dbContext, _access, _mockNotifier.Object, new Mock<ILogger<SetPokerSessionVisibilityCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ShouldMakeACompletedSessionPrivateAndRecheckAccess()
    {
        // Arrange
        var session = new PokerSessionFaker().WithStatus(PokerSessionStatus.Completed).Generate();
        _dbContext.AddPokerSession(session);

        // Act
        var result = await _handler.Handle(new SetPokerSessionVisibilityCommand(session.Id, Visibility.Private), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        session.Visibility.Should().Be(Visibility.Private);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyAccessChanged(session.Id), Times.Once);
    }

    [Fact]
    public async Task Handle_ByNonFacilitator_ShouldFail()
    {
        // Arrange
        var session = new PokerSessionFaker().Generate();
        _dbContext.AddPokerSession(session);
        _access.Actor = PokerSessionActors.Voter("someone");

        // Act
        var result = await _handler.Handle(new SetPokerSessionVisibilityCommand(session.Id, Visibility.Private), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(PokerSession.NotAFacilitatorError);
        session.Visibility.Should().Be(Visibility.Public);
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.VerifyNoOtherCalls();
    }

    public void Dispose() => _dbContext.Dispose();
}
