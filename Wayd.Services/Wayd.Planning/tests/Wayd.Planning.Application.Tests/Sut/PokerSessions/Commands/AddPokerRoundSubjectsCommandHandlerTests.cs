using Moq;
using Wayd.Common.Application.Requests.Planning.Commands;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Commands;

public sealed class AddPokerRoundSubjectsCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly FakePokerSessionAccess _access = new();
    private readonly Mock<IPokerSessionNotifier> _notifier = new();
    private readonly Guid _deskId = Guid.NewGuid();
    private readonly AddPokerRoundSubjectsCommandHandler _handler;

    public AddPokerRoundSubjectsCommandHandlerTests()
    {
        _handler = new AddPokerRoundSubjectsCommandHandler(_dbContext, _access, _notifier.Object);
    }

    private static PokerRoundSubjectInput ARequest(string key) =>
        new(PokerRoundSubjectKind.IntakeRequest, Guid.NewGuid(), key, $"Request {key}", null);

    private AddPokerRoundSubjectsCommand ForTheDesk(Guid sessionId, params PokerRoundSubjectInput[] subjects) =>
        new(sessionId, PokerSessionHostKind.IntakeDesk, _deskId, subjects);

    [Fact]
    public async Task Handle_ByADeskManager_AddsARoundPerRequestAndTellsTheSession()
    {
        // Arrange
        var session = new PokerSessionFaker().WithDeskHost(_deskId).WithFacilitators(Guid.NewGuid()).Generate();
        _dbContext.AddPokerSession(session);
        _access.Actor = PokerSessionActors.HostManager;

        // Act
        var result = await _handler.Handle(ForTheDesk(session.Id, ARequest("41"), ARequest("42")),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        session.Rounds.Select(r => r.Subject!.DisplayKey).Should().Equal("41", "42");
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _notifier.Verify(n => n.NotifyRoundAdded(session.Id, It.Is<PokerRoundDto>(r => r.Subject != null)), Times.Exactly(2));
    }

    [Fact]
    public async Task Handle_BySomeoneWhoDoesNotRunTheSession_FailsAndSavesNothing()
    {
        // Arrange
        var session = new PokerSessionFaker().WithDeskHost(_deskId).WithFacilitators(Guid.NewGuid()).Generate();
        _dbContext.AddPokerSession(session);
        _access.Actor = PokerSessionActors.Voter("someone");

        // Act
        var result = await _handler.Handle(ForTheDesk(session.Id, ARequest("41")),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.NotFoundError);
        _dbContext.SaveChangesCallCount.Should().Be(0);
        _notifier.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_ForAMissingSession_ReportsNotFound()
    {
        // Act
        var result = await _handler.Handle(ForTheDesk(Guid.NewGuid(), ARequest("41")),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.NotFoundError);
    }

    [Fact]
    public async Task Handle_ForAnotherDesksSession_FailsAndSavesNothing()
    {
        // Arrange — the records were checked against this desk, so they must not land in another's session
        var session = new PokerSessionFaker().WithDeskHost(Guid.NewGuid()).WithFacilitators(Guid.NewGuid()).Generate();
        _dbContext.AddPokerSession(session);
        _access.Actor = PokerSessionActors.HostManager;

        // Act
        var result = await _handler.Handle(ForTheDesk(session.Id, ARequest("41")), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AddPokerRoundSubjectsCommandHandler.NotThisHostsSessionError);
        session.Rounds.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAStandaloneSessionTheCallerRuns_FailsAndSavesNothing()
    {
        // Arrange
        var session = new PokerSessionFaker().WithVisibility(Visibility.Public).Generate();
        _dbContext.AddPokerSession(session);

        // Act
        var result = await _handler.Handle(ForTheDesk(session.Id, ARequest("41")), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(AddPokerRoundSubjectsCommandHandler.NotThisHostsSessionError);
        session.Rounds.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose() => _dbContext.Dispose();
}
