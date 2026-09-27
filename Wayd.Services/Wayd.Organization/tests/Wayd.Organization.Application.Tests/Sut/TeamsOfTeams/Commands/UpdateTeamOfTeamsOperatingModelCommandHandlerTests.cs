using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Identity;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Application.TeamsOfTeams.Commands;
using Wayd.Organization.TestData;
using Wayd.Tests.Shared;

namespace Wayd.Organization.Application.Tests.Sut.TeamsOfTeams.Commands;

public class UpdateTeamOfTeamsOperatingModelCommandHandlerTests : IDisposable
{
    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly TeamOfTeamsFaker _teamOfTeamsFaker = new();
    private readonly UpdateTeamOfTeamsOperatingModelCommandHandler _handler;

    public UpdateTeamOfTeamsOperatingModelCommandHandlerTests()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

        _handler = new UpdateTeamOfTeamsOperatingModelCommandHandler(
            _dbContext,
            new TestingDateTimeProvider(DateTime.UtcNow),
            currentUser.Object,
            Mock.Of<ILogger<UpdateTeamOfTeamsOperatingModelCommandHandler>>());
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_CorrectsTheTimeZone()
    {
        // Arrange
        var team = _teamOfTeamsFaker
            .WithOperatingModel(new TeamOfTeamsOperatingModelFaker().WithTimeZone("UTC"))
            .Generate();
        var operatingModel = team.OperatingModels.Single();
        _dbContext.AddTeamOfTeams(team);

        // Act
        var result = await _handler.Handle(new UpdateTeamOfTeamsOperatingModelCommand(team.Id, operatingModel.Id, "America/Denver"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        operatingModel.TimeZone.Should().Be("America/Denver");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_UnknownOperatingModel_ReturnsFailureWithoutSaving()
    {
        // Arrange
        var team = _teamOfTeamsFaker
            .WithOperatingModel(new TeamOfTeamsOperatingModelFaker())
            .Generate();
        _dbContext.AddTeamOfTeams(team);

        // Act
        var result = await _handler.Handle(new UpdateTeamOfTeamsOperatingModelCommand(team.Id, Guid.NewGuid(), "America/Denver"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_UnknownTeamOfTeams_ReturnsFailure()
    {
        // Arrange
        var teamId = Guid.NewGuid();

        // Act
        var result = await _handler.Handle(new UpdateTeamOfTeamsOperatingModelCommand(teamId, Guid.NewGuid(), "UTC"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"Team of Teams with Id {teamId} not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
