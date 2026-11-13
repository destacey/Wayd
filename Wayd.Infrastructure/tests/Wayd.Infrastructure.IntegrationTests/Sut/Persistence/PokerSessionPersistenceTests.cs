using Mapster;
using Mapster.Utils;
using Microsoft.EntityFrameworkCore;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Infrastructure.IntegrationTests.Infrastructure;
using Wayd.Planning.Application.PokerSessions;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Queries;
using Wayd.Planning.Domain.Enums;
using Wayd.Planning.Domain.Models.PlanningPoker;

namespace Wayd.Infrastructure.IntegrationTests.Sut.Persistence;

/// <summary>
/// Saves a poker session's facilitators, invites and members, and reads sessions back through the access filter
/// and the DTO projections. The fake contexts run both in memory, so only SQL Server shows they translate and that
/// the projections load the employees behind them.
/// </summary>
[Collection(nameof(SqlServerTestCollection))]
public sealed class PokerSessionPersistenceTests(SqlServerDbContextFixture fixture)
{
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 15, 9, 30, 0);

    private static readonly Lazy<bool> PlanningMappings = new(() =>
    {
        var assembly = typeof(PokerSessionDetailsDto).Assembly;
        TypeAdapterConfig.GlobalSettings.Scan(assembly);
        TypeAdapterConfig.GlobalSettings.ScanInheritedTypes(assembly);
        return true;
    });

    private readonly SqlServerDbContextFixture _fixture = fixture;

    private async Task<PokerSession> SaveSession(Visibility visibility, Guid facilitatorId, Action<PokerSession, PokerSessionActor> arrange, CancellationToken ct)
    {
        var scale = EstimationScale.Create($"Scale {Guid.NewGuid():N}"[..20], null,
            [new("1", true), new("2", true)], EventActor.System, Now).Value;
        var session = PokerSession.Create($"Session {Guid.NewGuid():N}", scale, visibility, facilitatorId, Now).Value;
        arrange(session, new PokerSessionActor(facilitatorId, "facilitator-user", false, new HashSet<Guid>()));

        await using var context = _fixture.CreateContext();
        context.EstimationScales.Add(scale);
        context.PokerSessions.Add(session);
        await context.SaveChangesAsync(ct);
        return session;
    }

    private static Mock<IPokerSessionAccess> ReaderOf(PokerSessionReader reader)
    {
        var access = new Mock<IPokerSessionAccess>();
        access.Setup(a => a.ResolveReader(It.IsAny<CancellationToken>())).ReturnsAsync(reader);
        return access;
    }

    [Fact]
    public async Task GetPokerSession_ProjectsFacilitatorsInvitesAndMembers()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _ = PlanningMappings.Value;
        var facilitator = await PlanningTestData.SeedEmployee(_fixture, ct);
        var invitee = await PlanningTestData.SeedEmployee(_fixture, ct);
        var session = await SaveSession(Visibility.Private, facilitator.Id, (s, actor) =>
        {
            s.SetInvitees([invitee.Id], [], actor).IsSuccess.Should().BeTrue();
            s.Join(false, new PokerSessionActor(invitee.Id, "invitee-user", false, new HashSet<Guid>()), Now)
                .IsSuccess.Should().BeTrue();
        }, ct);

        await using var read = _fixture.CreateContext();
        var handler = new GetPokerSessionQueryHandler(read, ReaderOf(new(facilitator.Id, false, [], false)).Object, Mock.Of<ICurrentUser>());

        // Act
        var dto = await handler.Handle(new GetPokerSessionQuery(session.Key.ToString()), ct);

        // Assert
        dto.Should().NotBeNull();
        dto!.Visibility.Should().Be(Visibility.Private);
        dto.Facilitators.Select(f => f.Id).Should().Equal(facilitator.Id);
        dto.InvitedEmployees.Select(e => e.Id).Should().Equal(invitee.Id);
        var member = dto.Members.Should().ContainSingle().Subject;
        member.Employee.Id.Should().Be(invitee.Id);
        member.Employee.Name.Should().NotBeNullOrWhiteSpace();
        member.IsVoting.Should().BeFalse();
        member.JoinedOn.Should().Be(Now);
        dto.CanManage.Should().BeTrue();
    }

    [Fact]
    public async Task GetPokerSession_ReturnsTheFocusedRound()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _ = PlanningMappings.Value;
        var facilitator = await PlanningTestData.SeedEmployee(_fixture, ct);
        Guid focusedRoundId = default;
        var session = await SaveSession(Visibility.Public, facilitator.Id, (s, actor) =>
        {
            s.AddRound("First", null, actor).IsSuccess.Should().BeTrue();
            focusedRoundId = s.AddRound("Second", null, actor).Value.Id;
            s.FocusRound(focusedRoundId, actor).IsSuccess.Should().BeTrue();
        }, ct);

        await using var read = _fixture.CreateContext();
        var handler = new GetPokerSessionQueryHandler(read, ReaderOf(new(facilitator.Id, false, [], false)).Object, Mock.Of<ICurrentUser>());

        // Act
        var dto = await handler.Handle(new GetPokerSessionQuery(session.Key.ToString()), ct);

        // Assert
        dto.Should().NotBeNull();
        dto!.Rounds.Should().HaveCount(2);
        dto.FocusedRoundId.Should().Be(focusedRoundId);
    }

    [Fact]
    public async Task GetPokerSessions_FiltersPrivateSessionsToThePeopleOnThem()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _ = PlanningMappings.Value;
        var facilitator = await PlanningTestData.SeedEmployee(_fixture, ct);
        var invitee = await PlanningTestData.SeedEmployee(_fixture, ct);
        var outsider = await PlanningTestData.SeedEmployee(_fixture, ct);
        var publicSession = await SaveSession(Visibility.Public, facilitator.Id, (_, _) => { }, ct);
        var privateSession = await SaveSession(Visibility.Private, facilitator.Id, (s, actor) =>
            s.SetInvitees([invitee.Id], [], actor).IsSuccess.Should().BeTrue(), ct);

        async Task<IReadOnlyCollection<Guid>> Listed(PokerSessionReader reader)
        {
            await using var read = _fixture.CreateContext();
            var sessions = await new GetPokerSessionsQueryHandler(read, ReaderOf(reader).Object).Handle(new GetPokerSessionsQuery(), ct);
            return [.. sessions.Select(s => s.Id).Where(id => id == publicSession.Id || id == privateSession.Id)];
        }

        // Act
        var forFacilitator = await Listed(new(facilitator.Id, false, [], false));
        var forInvitee = await Listed(new(invitee.Id, false, [Guid.NewGuid()], false));
        var forOutsider = await Listed(new(outsider.Id, false, [Guid.NewGuid()], false));
        var forNoEmployee = await Listed(new(null, false, [], false));
        var forAdministrator = await Listed(new(outsider.Id, true, [], false));

        // Assert
        forFacilitator.Should().BeEquivalentTo([publicSession.Id, privateSession.Id]);
        forInvitee.Should().BeEquivalentTo([publicSession.Id, privateSession.Id]);
        forOutsider.Should().Equal(publicSession.Id);
        forNoEmployee.Should().Equal(publicSession.Id);
        forAdministrator.Should().BeEquivalentTo([publicSession.Id, privateSession.Id]);
    }

    [Fact]
    public async Task GetPokerSessions_AdmitsAMemberOfAnInvitedTeam_AndProjectsTeamsAndParticipants()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _ = PlanningMappings.Value;
        var facilitator = await PlanningTestData.SeedEmployee(_fixture, ct);
        var teammate = await PlanningTestData.SeedEmployee(_fixture, ct);
        var team = await PlanningTestData.SeedTeam(_fixture, ct);
        var session = await SaveSession(Visibility.Private, facilitator.Id, (s, actor) =>
        {
            s.SetInvitees([], [team.Id], actor).IsSuccess.Should().BeTrue();
            s.Join(true, new PokerSessionActor(teammate.Id, "teammate-user", false, new HashSet<Guid> { team.Id }), Now)
                .IsSuccess.Should().BeTrue();
        }, ct);

        // Act
        await using var listRead = _fixture.CreateContext();
        var listed = await new GetPokerSessionsQueryHandler(listRead, ReaderOf(new(teammate.Id, false, [team.Id], false)).Object)
            .Handle(new GetPokerSessionsQuery(), ct);
        await using var detailRead = _fixture.CreateContext();
        var details = await new GetPokerSessionQueryHandler(detailRead, ReaderOf(new(teammate.Id, false, [team.Id], false)).Object, Mock.Of<ICurrentUser>())
            .Handle(new GetPokerSessionQuery(session.Key.ToString()), ct);

        // Assert
        var row = listed.Should().ContainSingle(s => s.Id == session.Id).Subject;
        row.Participants.Select(p => p.Id).Should().Equal(teammate.Id);
        row.CanManage.Should().BeFalse();
        details.Should().NotBeNull();
        var invited = details!.InvitedTeams.Should().ContainSingle().Subject;
        invited.Id.Should().Be(team.Id);
        invited.Name.Should().Be(team.Name);
    }

    private async Task<PokerSession> SaveHostedSession(Guid deskId, Guid facilitatorId, PokerRoundRecord record, CancellationToken ct)
    {
        var scale = EstimationScale.Create($"Scale {Guid.NewGuid():N}"[..20], null,
            [new("S", true), new("M", true), new("?", false)], EventActor.System, Now).Value;
        var host = new PokerSessionHost(PokerSessionHostKind.IntakeDesk, deskId);
        var session = PokerSession.CreateHosted($"Session {Guid.NewGuid():N}", scale.CurrentVersion, host, facilitatorId, Now).Value;
        var actor = new PokerSessionActor(facilitatorId, "facilitator-user", false, new HashSet<Guid>(), ManagesHost: true);
        session.AddSubjectRounds([record], actor).IsSuccess.Should().BeTrue();

        await using var context = _fixture.CreateContext();
        context.EstimationScales.Add(scale);
        context.PokerSessions.Add(session);
        await context.SaveChangesAsync(ct);
        return session;
    }

    [Fact]
    public async Task HostedSession_SavesItsHostAndRoundSubjects_AndListsOnlyUnderItsHost()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _ = PlanningMappings.Value;
        var creator = await PlanningTestData.SeedEmployee(_fixture, ct);
        var deskAgent = await PlanningTestData.SeedEmployee(_fixture, ct);
        var outsider = await PlanningTestData.SeedEmployee(_fixture, ct);
        var deskId = Guid.NewGuid();
        var subject = new PokerRoundSubject(PokerRoundSubjectKind.IntakeRequest, Guid.NewGuid(), "42");
        var hosted = await SaveHostedSession(deskId, creator.Id, new(subject, "Export invoices", "Finance needs a monthly export."), ct);
        var agentReader = new PokerSessionReader(deskAgent.Id, false, [], false, [deskId]);

        async Task<IReadOnlyCollection<Guid>> Listed(PokerSessionReader reader, GetPokerSessionsQuery query)
        {
            await using var read = _fixture.CreateContext();
            var sessions = await new GetPokerSessionsQueryHandler(read, ReaderOf(reader).Object).Handle(query, ct);
            return [.. sessions.Select(s => s.Id)];
        }

        var underTheDesk = new GetPokerSessionsQuery(HostKind: PokerSessionHostKind.IntakeDesk, HostId: deskId);

        // Act
        await using var read = _fixture.CreateContext();
        var details = await new GetPokerSessionQueryHandler(read, ReaderOf(agentReader).Object, Mock.Of<ICurrentUser>())
            .Handle(new GetPokerSessionQuery(hosted.Key.ToString()), ct);
        var standaloneForAdministrator = await Listed(new(outsider.Id, true, [], false), new GetPokerSessionsQuery());
        var underTheDeskForAgent = await Listed(agentReader, underTheDesk);
        var underTheDeskForOutsider = await Listed(new(outsider.Id, false, [], false), underTheDesk);

        // Assert
        details.Should().NotBeNull();
        details!.Host.Should().BeEquivalentTo(new PokerSessionHostDto { Kind = PokerSessionHostKind.IntakeDesk, Id = deskId });
        details.CanManage.Should().BeTrue();
        var round = details.Rounds.Should().ContainSingle().Subject;
        round.Subject.Should().BeEquivalentTo(new PokerRoundSubjectDto
        {
            Kind = PokerRoundSubjectKind.IntakeRequest,
            Id = subject.Id,
            DisplayKey = "42",
        });
        round.Label.Should().Be("Export invoices");
        round.Description.Should().Be("Finance needs a monthly export.");
        standaloneForAdministrator.Should().NotContain(hosted.Id);
        underTheDeskForAgent.Should().Equal(hosted.Id);
        underTheDeskForOutsider.Should().BeEmpty();
    }

    [Fact]
    public async Task RoundPublication_SavesARefusalThenItsReplacement_AndReadsBackOnTheSession()
    {
        // Arrange — a refusal saved first, so publishing replaces a stored publication rather than adding one
        var ct = TestContext.Current.CancellationToken;
        _ = PlanningMappings.Value;
        var agent = await PlanningTestData.SeedEmployee(_fixture, ct);
        var deskId = Guid.NewGuid();
        var subject = new PokerRoundSubject(PokerRoundSubjectKind.IntakeRequest, Guid.NewGuid(), "42");
        var hosted = await SaveHostedSession(deskId, agent.Id, new(subject, "Export invoices", null), ct);
        var actor = new PokerSessionActor(agent.Id, "facilitator-user", false, new HashSet<Guid>(), ManagesHost: true);
        var reader = new PokerSessionReader(agent.Id, false, [], false, [deskId]);
        var refusedAt = Now + Duration.FromMinutes(5);
        var publishedAt = Now + Duration.FromMinutes(10);

        async Task Change(Action<PokerSession, PokerRound> change)
        {
            await using var context = _fixture.CreateContext();
            var session = await context.PokerSessions.IncludeAccess().Include(s => s.Rounds).ThenInclude(r => r.Votes)
                .SingleAsync(s => s.Id == hosted.Id, ct);
            change(session, session.Rounds.Single());
            await context.SaveChangesAsync(ct);
        }

        await Change((session, round) => session.RevealRound(round.Id, actor).IsSuccess.Should().BeTrue());
        await using (var context = _fixture.CreateContext())
        {
            var session = await context.PokerSessions.IncludeAccess().Include(s => s.Rounds).SingleAsync(s => s.Id == hosted.Id, ct);
            var version = await context.EstimationScaleVersions.SingleAsync(v => v.Id == session.EstimationScaleVersionId, ct);
            session.SetConsensus(session.Rounds.Single().Id, "M", version, actor).IsSuccess.Should().BeTrue();
            await context.SaveChangesAsync(ct);
        }
        await Change((session, round) =>
            session.RecordRoundPublication(round.Id, CSharpFunctionalExtensions.Result.Failure("The request has moved to another desk."), actor, refusedAt));

        async Task<PokerRoundDto> TheRound()
        {
            await using var read = _fixture.CreateContext();
            var details = await new GetPokerSessionQueryHandler(read, ReaderOf(reader).Object, Mock.Of<ICurrentUser>())
                .Handle(new GetPokerSessionQuery(hosted.Key.ToString()), ct);
            return details!.Rounds.Should().ContainSingle().Subject;
        }

        // Act
        var whileRefused = await TheRound();
        await Change((session, round) =>
            session.RecordRoundPublication(round.Id, CSharpFunctionalExtensions.Result.Success(), actor, publishedAt));
        var oncePublished = await TheRound();

        // Assert
        whileRefused.ConsensusEstimate.Should().Be("M");
        whileRefused.Status.Should().Be("Accepted");
        whileRefused.Publication!.Outcome.Should().Be(PokerRoundPublicationOutcome.Refused);
        whileRefused.Publication.RefusalReason.Should().Be("The request has moved to another desk.");
        whileRefused.Publication.At.Should().Be(refusedAt);
        whileRefused.Publication.By!.Id.Should().Be(agent.Id);

        oncePublished.Publication!.Outcome.Should().Be(PokerRoundPublicationOutcome.Published);
        oncePublished.Publication.RefusalReason.Should().BeNull();
        oncePublished.Publication.At.Should().Be(publishedAt);
        oncePublished.Publication.By!.Id.Should().Be(agent.Id);
    }

    [Fact]
    public async Task StandaloneSession_ReadsBackWithNoHostAndNoRoundSubject()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        _ = PlanningMappings.Value;
        var facilitator = await PlanningTestData.SeedEmployee(_fixture, ct);
        var session = await SaveSession(Visibility.Public, facilitator.Id, (s, actor) =>
            s.AddRound("Story", null, actor).IsSuccess.Should().BeTrue(), ct);

        // Act
        await using var read = _fixture.CreateContext();
        var stored = await read.PokerSessions.Include(s => s.Rounds).SingleAsync(s => s.Id == session.Id, ct);

        // Assert
        stored.Host.Should().BeNull();
        stored.Rounds.Should().ContainSingle().Which.Subject.Should().BeNull();
    }
}
