using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.PokerSessions;
using Wayd.Planning.Application.PokerSessions.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.PlanningPoker;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Queries;

/// <summary>
/// Covers which sessions the list shows: every public one, and the private ones the reader is on.
/// </summary>
public class GetPokerSessionsQueryHandlerTests : IDisposable
{
    private readonly FakePokerSessionAccess _access = new();
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly GetPokerSessionsQueryHandler _handler;

    private readonly Guid _invitedEmployeeId = Guid.NewGuid();
    private readonly Guid _invitedTeamId = Guid.NewGuid();
    private readonly PokerSession _public;
    private readonly PokerSession _privateWithInvites;
    private readonly PokerSession _privateOfSomeoneElse;

    public GetPokerSessionsQueryHandlerTests()
    {
        _public = new PokerSessionFaker().WithKey(1).Generate();
        _privateWithInvites = new PokerSessionFaker().WithKey(2).WithVisibility(Visibility.Private)
            .WithFacilitators(Guid.NewGuid())
            .WithInvitedEmployees(_invitedEmployeeId)
            .WithInvitedTeams(_invitedTeamId)
            .Generate();
        _privateOfSomeoneElse = new PokerSessionFaker().WithKey(3).WithVisibility(Visibility.Private)
            .WithFacilitators(Guid.NewGuid())
            .Generate();
        _dbContext.AddPokerSessions([_public.WithEmployeesLinked(), _privateWithInvites.WithEmployeesLinked(), _privateOfSomeoneElse.WithEmployeesLinked()]);

        _handler = new GetPokerSessionsQueryHandler(_dbContext, _access);
    }

    [Theory]
    [InlineData("invited employee", 2)]
    [InlineData("invited team member", 2)]
    [InlineData("outsider", 1)]
    [InlineData("no employee", 1)]
    [InlineData("administrator", 3)]
    public async Task Handle_ShouldListPublicSessionsAndThePrivateOnesTheReaderIsOn(string reader, int expected)
    {
        // Arrange
        _access.Reader = reader switch
        {
            "invited employee" => new PokerSessionReader(_invitedEmployeeId, false, [], false),
            "invited team member" => new PokerSessionReader(Guid.NewGuid(), false, [_invitedTeamId], false),
            "outsider" => new PokerSessionReader(Guid.NewGuid(), false, [], false),
            "no employee" => new PokerSessionReader(null, false, [], false),
            _ => new PokerSessionReader(Guid.NewGuid(), true, [], false),
        };

        // Act
        var result = await _handler.Handle(new GetPokerSessionsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().HaveCount(expected);
        result.Should().Contain(s => s.Id == _public.Id);
        if (expected == 1)
            result.Should().NotContain(s => s.Id == _privateWithInvites.Id);
    }

    public void Dispose() => _dbContext.Dispose();
}
