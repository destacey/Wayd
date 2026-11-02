using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Commands;

public class SetPokerSessionFacilitatorsCommandHandlerTests : IDisposable
{
    private readonly FakePokerSessionAccess _access = new();
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<IPokerSessionNotifier> _mockNotifier = new();
    private readonly SetPokerSessionFacilitatorsCommandHandler _handler;

    public SetPokerSessionFacilitatorsCommandHandlerTests()
    {
        _handler = new SetPokerSessionFacilitatorsCommandHandler(_dbContext, _access, _mockNotifier.Object, new Mock<ILogger<SetPokerSessionFacilitatorsCommandHandler>>().Object);
    }

    [Fact]
    public async Task Handle_ShouldReplaceTheFacilitators()
    {
        // Arrange
        var session = new PokerSessionFaker().Generate();
        _dbContext.AddPokerSession(session);
        var other = new EmployeeFaker().Generate();
        _dbContext.AddEmployees([other, new EmployeeFaker().WithId(PokerSessionActors.FacilitatorEmployeeId).Generate()]);

        // Act
        var result = await _handler.Handle(new SetPokerSessionFacilitatorsCommand(session.Id, [PokerSessionActors.FacilitatorEmployeeId, other.Id]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        session.Facilitators.Select(f => f.EmployeeId).Should().BeEquivalentTo([PokerSessionActors.FacilitatorEmployeeId, other.Id]);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _mockNotifier.Verify(n => n.NotifyAccessChanged(session.Id), Times.Once);
    }

    [Fact]
    public async Task Handle_UnknownEmployee_ShouldFail()
    {
        // Arrange
        var session = new PokerSessionFaker().Generate();
        _dbContext.AddPokerSession(session);

        // Act
        var result = await _handler.Handle(new SetPokerSessionFacilitatorsCommand(session.Id, [Guid.NewGuid()]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        session.IsFacilitator(PokerSessionActors.FacilitatorEmployeeId).Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ByNonFacilitator_ShouldFailBeforeCheckingTheEmployees()
    {
        // Arrange
        var session = new PokerSessionFaker().Generate();
        _dbContext.AddPokerSession(session);
        _access.Actor = PokerSessionActors.Voter("someone");

        // Act
        var result = await _handler.Handle(new SetPokerSessionFacilitatorsCommand(session.Id, [Guid.NewGuid()]), TestContext.Current.CancellationToken);

        // Assert
        result.Error.Should().Be(PokerSession.NotAFacilitatorError);
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _mockNotifier.VerifyNoOtherCalls();
    }

    public void Dispose() => _dbContext.Dispose();
}
