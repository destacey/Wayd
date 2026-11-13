using CSharpFunctionalExtensions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.DemandManagement;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Commands;

public sealed class PublishPokerRoundsCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly FakePokerSessionAccess _access = new() { Actor = PokerSessionActors.HostManager };
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly Mock<IPokerSessionNotifier> _notifier = new();
    private readonly TestingDateTimeProvider _clock = new(new FakeClock(Instant.FromUtc(2026, 10, 1, 8, 0, 0)));
    private readonly EstimationScale _scale = new EstimationScaleFaker().WithValues("S", "M", "L", "?").Generate();
    private readonly Guid _deskId = Guid.NewGuid();
    private readonly PublishPokerRoundsCommandHandler _handler;

    public PublishPokerRoundsCommandHandlerTests()
    {
        _handler = new PublishPokerRoundsCommandHandler(_dbContext, _access, _dispatcher.Object, _clock, _notifier.Object);
        DemandAnswers(Result.Success());
    }

    public void Dispose() => _dbContext.Dispose();

    private void DemandAnswers(Result answer) =>
        _dispatcher.Setup(d => d.Send(It.IsAny<PublishPokerRoundToIntakeRequestCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(answer);

    private PokerSession ADeskSession()
    {
        var session = PokerSession.CreateHosted("Desk Refinement", _scale.CurrentVersion,
            new PokerSessionHost(PokerSessionHostKind.IntakeDesk, _deskId), PokerSessionActors.FacilitatorEmployeeId, _clock.Now).Value;
        _dbContext.AddPokerSession(session);
        return session;
    }

    private PokerRound AnAcceptedRequestRound(PokerSession session, string key, string consensus)
    {
        var record = new PokerRoundRecord(new PokerRoundSubject(PokerRoundSubjectKind.IntakeRequest, Guid.NewGuid(), key), $"Request {key}", null);
        var round = session.AddSubjectRounds([record], PokerSessionActors.HostManager).Value.Single();
        session.RevealRound(round.Id, PokerSessionActors.HostManager);
        session.SetConsensus(round.Id, consensus, _scale.CurrentVersion, PokerSessionActors.HostManager);
        return round;
    }

    [Fact]
    public async Task Handle_ForOneRound_SendsItsConsensusToDemandAndMarksItPublished()
    {
        // Arrange
        var session = ADeskSession();
        var round = AnAcceptedRequestRound(session, "41", "M");

        // Act
        var result = await _handler.Handle(new PublishPokerRoundsCommand(session.Id, round.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dispatcher.Verify(d => d.Send(
            new PublishPokerRoundToIntakeRequestCommand(round.Subject!.Id, _deskId, _scale.CurrentVersion.Id, "M", session.Id, round.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        round.Publication!.Outcome.Should().Be(PokerRoundPublicationOutcome.Published);
        round.Publication.At.Should().Be(_clock.Now);
        round.Publication.ByEmployeeId.Should().Be(PokerSessionActors.HostManager.EmployeeId);
        result.Value.Should().ContainSingle().Which.Publication!.Outcome.Should().Be(PokerRoundPublicationOutcome.Published);
        _dbContext.SaveChangesCallCount.Should().Be(1);
        _notifier.Verify(n => n.NotifyRoundPublicationChanged(session.Id, round.Id,
            It.Is<PokerRoundPublicationDto>(p => p.Outcome == PokerRoundPublicationOutcome.Published)), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenDemandRefuses_KeepsTheReasonOnTheRoundAndStillSucceeds()
    {
        // Arrange
        var session = ADeskSession();
        var round = AnAcceptedRequestRound(session, "41", "M");
        DemandAnswers(Result.Failure("The request has moved to another desk."));

        // Act
        var result = await _handler.Handle(new PublishPokerRoundsCommand(session.Id, round.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        round.Publication!.Outcome.Should().Be(PokerRoundPublicationOutcome.Refused);
        round.Publication.RefusalReason.Should().Be("The request has moved to another desk.");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithNoRoundNamed_PublishesEveryAcceptedRoundAndSkipsTheRest()
    {
        // Arrange — two accepted, one still voting
        var session = ADeskSession();
        var first = AnAcceptedRequestRound(session, "41", "S");
        var second = AnAcceptedRequestRound(session, "42", "?");
        var voting = session.AddSubjectRounds(
            [new PokerRoundRecord(new PokerRoundSubject(PokerRoundSubjectKind.IntakeRequest, Guid.NewGuid(), "43"), "Request 43", null)],
            PokerSessionActors.HostManager).Value.Single();

        // Act
        var result = await _handler.Handle(new PublishPokerRoundsCommand(session.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Select(r => r.Id).Should().Equal(first.Id, second.Id);
        voting.Publication.Should().BeNull();
        _dispatcher.Verify(d => d.Send(It.Is<PublishPokerRoundToIntakeRequestCommand>(c => c.Value == "?" && c.PokerRoundId == second.Id),
            It.IsAny<CancellationToken>()), Times.Once);
        _dispatcher.Verify(d => d.Send(It.IsAny<PublishPokerRoundToIntakeRequestCommand>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_BySomeoneWhoDoesNotRunTheSession_FailsAndSendsNothing()
    {
        // Arrange
        var session = ADeskSession();
        var round = AnAcceptedRequestRound(session, "41", "M");
        _access.Actor = PokerSessionActors.Voter("someone");

        // Act
        var result = await _handler.Handle(new PublishPokerRoundsCommand(session.Id, round.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.NotFoundError);
        round.Publication.Should().BeNull();
        _dispatcher.VerifyNoOtherCalls();
        _notifier.VerifyNoOtherCalls();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForARoundAlreadyPublished_FailsAndSendsNothing()
    {
        // Arrange
        var session = ADeskSession();
        var round = AnAcceptedRequestRound(session, "41", "M");
        session.RecordRoundPublication(round.Id, Result.Success(), PokerSessionActors.HostManager, _clock.Now);

        // Act
        var result = await _handler.Handle(new PublishPokerRoundsCommand(session.Id, round.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.RoundAlreadyPublishedError);
        _dispatcher.VerifyNoOtherCalls();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAMissingSession_ReportsNotFound()
    {
        // Act
        var result = await _handler.Handle(new PublishPokerRoundsCommand(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(PokerSession.NotFoundError);
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
