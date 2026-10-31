using Wayd.Common.Application.Requests.Organization;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Organization.Application.Teams.Queries;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Tests.Data;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Queries;

public sealed class GetTeamsByIdOrCodeQueryHandlerTests : IDisposable
{
    private readonly FakeOrganizationDbContext _dbContext = new();

    private GetTeamsByIdOrCodeQueryHandler CreateHandler() => new(_dbContext);

    [Fact]
    public async Task Handle_ShouldFindTeamsAndTeamsOfTeams_ByIdOrByCodeInAnyCase()
    {
        // Arrange
        var team = new TeamFaker().WithCode(new TeamCode("PAY")).Generate();
        var teamOfTeams = new TeamOfTeamsFaker().WithCode(new TeamCode("ART1")).Generate();
        _dbContext.AddTeam(team);
        _dbContext.AddTeamOfTeams(teamOfTeams);
        _dbContext.AddTeam(new TeamFaker().WithCode(new TeamCode("OTHER")).Generate());

        // Act
        var result = await CreateHandler().Handle(new GetTeamsByIdOrCodeQuery([teamOfTeams.Id], [" pay "]), TestContext.Current.CancellationToken);

        // Assert
        result.Select(t => t.Id).Should().BeEquivalentTo([team.Id, teamOfTeams.Id]);
    }

    [Fact]
    public async Task Handle_ShouldListATeamNamedBothWaysOnce()
    {
        // Arrange
        var team = new TeamFaker().WithCode(new TeamCode("PAY")).Generate();
        _dbContext.AddTeam(team);

        // Act
        var result = await CreateHandler().Handle(new GetTeamsByIdOrCodeQuery([team.Id], ["PAY"]), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.Id.Should().Be(team.Id);
    }

    [Fact]
    public async Task Handle_ShouldIncludeInactiveTeams_WithTheirState()
    {
        // Arrange
        var team = new TeamFaker().WithCode(new TeamCode("OLD")).WithIsActive(false).Generate();
        _dbContext.AddTeam(team);

        // Act
        var result = await CreateHandler().Handle(new GetTeamsByIdOrCodeQuery([], ["OLD"]), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutUnknownIdsAndUnknownOrMalformedCodes()
    {
        // Arrange
        _dbContext.AddTeam(new TeamFaker().WithCode(new TeamCode("PAY")).Generate());

        // Act
        var result = await CreateHandler().Handle(
            new GetTeamsByIdOrCodeQuery([Guid.NewGuid(), Guid.Empty], ["NOPE", "not a code", ""]),
            TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    public void Dispose() => _dbContext.Dispose();
}
