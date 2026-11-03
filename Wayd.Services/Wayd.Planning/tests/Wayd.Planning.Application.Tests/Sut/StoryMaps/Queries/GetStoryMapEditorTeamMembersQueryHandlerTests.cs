using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Enums;
using Wayd.Planning.Application.StoryMaps;
using Wayd.Planning.Application.StoryMaps.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;

namespace Wayd.Planning.Application.Tests.Sut.StoryMaps.Queries;

public class GetStoryMapEditorTeamMembersQueryHandlerTests : IDisposable
{
    private readonly FakeStoryMapAccess _access = new();
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<IDispatcher> _dispatcher = new();
    private readonly GetStoryMapEditorTeamMembersQueryHandler _handler;
    private readonly Guid _teamId = Guid.NewGuid();

    public GetStoryMapEditorTeamMembersQueryHandlerTests()
    {
        _handler = new GetStoryMapEditorTeamMembersQueryHandler(_dbContext, _access, _dispatcher.Object);
    }

    [Fact]
    public async Task Handle_ShouldListTheTeamsActiveMembersByName()
    {
        // Arrange — on a public map too, since its editor teams decide who edits it.
        var map = new StoryMapFaker().WithEditorTeams(_teamId).Generate();
        _dbContext.AddStoryMap(map);
        var zoe = new EmployeeFaker().Generate();
        var adam = new EmployeeFaker().Generate();
        var inactive = new EmployeeFaker().RuleFor(e => e.IsActive, false).Generate();
        _dbContext.AddEmployees([zoe, adam, inactive]);
        _dispatcher
            .Setup(d => d.Send(It.Is<GetTeamMemberIdsQuery>(q => q.TeamId == _teamId), It.IsAny<CancellationToken>()))
            .ReturnsAsync([zoe.Id, adam.Id, inactive.Id]);

        // Act
        var result = await _handler.Handle(new GetStoryMapEditorTeamMembersQuery(map.Id, _teamId), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Select(m => m.Id).Should().BeEquivalentTo([zoe.Id, adam.Id]);
        result.Select(m => m.Name).Should().BeInAscendingOrder(StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Handle_TeamNotAnEditor_ShouldReturnNull()
    {
        // Arrange
        var map = new StoryMapFaker().Generate();
        _dbContext.AddStoryMap(map);

        // Act
        var result = await _handler.Handle(new GetStoryMapEditorTeamMembersQuery(map.Id, _teamId), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        _dispatcher.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Handle_PrivateMapTheReaderCannotSee_ShouldReturnNull()
    {
        // Arrange
        var map = new StoryMapFaker().WithVisibility(Visibility.Private).WithEditorTeams(_teamId).Generate();
        _dbContext.AddStoryMap(map);
        _access.Reader = new StoryMapReader(StoryMapActors.Viewer.EmployeeId, false, [], false);

        // Act
        var result = await _handler.Handle(new GetStoryMapEditorTeamMembersQuery(map.Id, _teamId), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
        _dispatcher.VerifyNoOtherCalls();
    }

    public void Dispose() => _dbContext.Dispose();
}
