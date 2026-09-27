using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Identity;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Application.TeamsOfTeams.Commands;
using Wayd.Organization.TestData;
using Wayd.Tests.Shared;

namespace Wayd.Organization.Application.Tests.Sut.TeamsOfTeams.Commands;

public class SetTeamOfTeamsOperatingModelCommandHandlerTests : IDisposable
{
    private static readonly LocalDate ActiveDate = new(2025, 1, 1);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly TeamOfTeamsFaker _teamOfTeamsFaker = new();
    private readonly SetTeamOfTeamsOperatingModelCommandHandler _handler;

    public SetTeamOfTeamsOperatingModelCommandHandlerTests()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

        _handler = new SetTeamOfTeamsOperatingModelCommandHandler(
            _dbContext,
            new TestingDateTimeProvider(DateTime.UtcNow),
            currentUser.Object,
            Mock.Of<ILogger<SetTeamOfTeamsOperatingModelCommandHandler>>());
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_OpensANewModelAndClosesTheCurrentOne()
    {
        // Arrange
        var team = _teamOfTeamsFaker
            .WithOperatingModel(new TeamOfTeamsOperatingModelFaker().WithTimeZone("UTC"), ActiveDate)
            .Generate();
        var current = team.OperatingModels.Single();
        _dbContext.AddTeamOfTeams(team);
        var moveDate = ActiveDate.PlusMonths(6);

        // Act
        var result = await _handler.Handle(new SetTeamOfTeamsOperatingModelCommand(team.Id, moveDate, "America/Chicago"), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.OperatingModels.Should().HaveCount(2);
        current.DateRange.End.Should().Be(moveDate.PlusDays(-1));
        current.TimeZone.Should().Be("UTC");
        team.OperatingModels.Single(m => m.IsCurrent).TimeZone.Should().Be("America/Chicago");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenTheDomainRefuses_ReturnsFailureWithoutSaving()
    {
        // Arrange
        var team = _teamOfTeamsFaker
            .WithOperatingModel(new TeamOfTeamsOperatingModelFaker(), ActiveDate)
            .Generate();
        _dbContext.AddTeamOfTeams(team);

        // Act
        var result = await _handler.Handle(new SetTeamOfTeamsOperatingModelCommand(team.Id, ActiveDate, "America/Chicago"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("New operating model start date must be after the current model's start date.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_UnknownTeamOfTeams_ReturnsFailure()
    {
        // Arrange
        var teamId = Guid.NewGuid();

        // Act
        var result = await _handler.Handle(new SetTeamOfTeamsOperatingModelCommand(teamId, ActiveDate, "UTC"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"Team of Teams with Id {teamId} not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
