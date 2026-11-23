using Microsoft.EntityFrameworkCore;
using Moq;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Planning.Application.PokerSessions;
using Wayd.Planning.Application.PokerSessions.Commands;
using Wayd.Planning.Application.PokerSessions.Dtos;
using Wayd.Planning.Application.PokerSessions.Interfaces;
using Wayd.Planning.IntegrationTests.Infrastructure;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// Integration tests for <see cref="JoinPokerSessionCommandHandler"/> against a real SQL Server container, where a
/// second join meets the first at the member table's key and is retried against a reload that only a real change
/// tracker can get wrong.
/// </summary>
[Collection(SqlServerTestCollection.Name)]
public sealed class JoinPokerSessionCommandHandlerTests
{
    private readonly SqlServerDbContextFixture _fixture;

    public JoinPokerSessionCommandHandlerTests(SqlServerDbContextFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Handle_WhenAnotherJoinSavedFirst_SucceedsWithTheOneMember()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var seed = await PokerSeed.ActiveSession(_fixture, ct);
        var competitor = new CompetingSave(async token =>
        {
            await using var other = _fixture.CreateContext();
            var session = await PokerSeed.Load(other, seed.SessionId, token);
            session.Join(true, seed.Voter, SqlServerDbContextFixture.FixedNow).IsSuccess.Should().BeTrue();
            await other.SaveChangesAsync(token);
        });
        var access = new Mock<IPokerSessionAccess>();
        access.Setup(a => a.ResolveActor(It.IsAny<Domain.Models.PlanningPoker.PokerSession>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(seed.Voter);
        var notifier = new Mock<IPokerSessionNotifier>();

        // Act
        CSharpFunctionalExtensions.Result<PokerSessionMemberDto> result;
        await using (var context = _fixture.CreateContext(competitor))
        {
            result = await new JoinPokerSessionCommandHandler(context, access.Object, SqlServerDbContextFixture.DateTimeProvider(), notifier.Object)
                .Handle(new JoinPokerSessionCommand(seed.SessionId, IsVoting: false), ct);
        }

        // Assert
        competitor.Made.Should().BeTrue();
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Employee.Id.Should().Be(seed.Voter.EmployeeId);
        await using var verify = _fixture.CreateContext();
        var members = await Members(verify, seed.SessionId, ct);
        members.Should().ContainSingle().Which.Should().Be((seed.Voter.EmployeeId, true));
        notifier.Verify(n => n.NotifyMemberUpdated(seed.SessionId, It.IsAny<PokerSessionMemberDto>()), Times.Once);
    }

    private static async Task<List<(Guid EmployeeId, bool IsVoting)>> Members(WaydDbContext context, Guid sessionId, CancellationToken ct) =>
        [.. (await PokerSeed.Load(context, sessionId, ct)).Members.Select(m => (m.EmployeeId, m.IsVoting))];
}
