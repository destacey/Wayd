using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.PokerSessions;
using Wayd.Planning.Application.PokerSessions.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.PokerSessions.Queries;

public class GetPokerSessionInvitedTeamMembersQueryHandlerTests : IDisposable
{
    private readonly FakePokerSessionAccess _access = new();
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly GetPokerSessionInvitedTeamMembersQueryHandler _handler;
    private readonly Guid _teamId = Guid.NewGuid();

    public GetPokerSessionInvitedTeamMembersQueryHandlerTests()
    {
        _handler = new GetPokerSessionInvitedTeamMembersQueryHandler(_dbContext, _access, _dispatcher.Object);
    }

    [Fact]
    public async Task Handle_ShouldListTheTeamsActiveMembersByName()
    {
        // Arrange
        var session = new PokerSessionFaker().WithVisibility(Visibility.Private).WithInvitedTeams(_teamId).Generate();
        _dbContext.AddPokerSession(session);
        var zoe = new EmployeeFaker().Generate();
        var adam = new EmployeeFaker().Generate();
        var inactive = new EmployeeFaker().RuleFor(e => e.IsActive, false).Generate();
        _dbContext.AddEmployees([zoe, adam, inactive]);
        _dispatcher
            .Setup(d => d.Send(It.Is<GetTeamMemberIdsQuery>(q => q.TeamId == _teamId), It.IsAny<CancellationToken>()))
            .ReturnsAsync([zoe.Id, adam.Id, inactive.Id]);

        // Act
        var result = await _handler.Handle(new GetPokerSessionInvitedTeamMembersQuery(session.Id, _teamId), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Select(m => m.Id).Should().BeEquivalentTo([zoe.Id, adam.Id]);
        result.Select(m => m.Name).Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_PublicSession_ShouldReturnNull()
    {
        // Arrange
        var session = new PokerSessionFaker().WithInvitedTeams(_teamId).Generate();
        _dbContext.AddPokerSession(session);

        // Act
        var result = await _handler.Handle(new GetPokerSessionInvitedTeamMembersQuery(session.Id, _teamId), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_TeamNotInvited_ShouldReturnNull()
    {
        // Arrange
        var session = new PokerSessionFaker().WithVisibility(Visibility.Private).Generate();
        _dbContext.AddPokerSession(session);

        // Act
        var result = await _handler.Handle(new GetPokerSessionInvitedTeamMembersQuery(session.Id, _teamId), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_SessionTheCallerCannotSee_ShouldReturnNull()
    {
        // Arrange
        var session = new PokerSessionFaker().WithVisibility(Visibility.Private).WithFacilitators(Guid.NewGuid()).WithInvitedTeams(_teamId).Generate();
        _dbContext.AddPokerSession(session);
        _access.Reader = new PokerSessionReader(Guid.NewGuid(), false, [], false);

        // Act
        var result = await _handler.Handle(new GetPokerSessionInvitedTeamMembersQuery(session.Id, _teamId), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        _dispatcher.VerifyNoOtherCalls();
    }

    public void Dispose() => _dbContext.Dispose();
}
