using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Identity;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Application.TeamsOfTeams.Commands;
using Wayd.Organization.TestData;
using Wayd.Tests.Shared;
using Wayd.Tests.Shared.Extensions;

namespace Wayd.Organization.Application.Tests.Sut.TeamsOfTeams.Commands;

public class DeleteTeamOfTeamsOperatingModelCommandHandlerTests : IDisposable
{
    private static readonly LocalDate ActiveDate = new(2025, 1, 1);

    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly TeamOfTeamsFaker _teamOfTeamsFaker = new();
    private readonly TestingDateTimeProvider _dateTimeProvider = new(DateTime.UtcNow);
    private readonly DeleteTeamOfTeamsOperatingModelCommandHandler _handler;

    public DeleteTeamOfTeamsOperatingModelCommandHandlerTests()
    {
        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

        _handler = new DeleteTeamOfTeamsOperatingModelCommandHandler(
            _dbContext,
            _dateTimeProvider,
            currentUser.Object,
            Mock.Of<ILogger<DeleteTeamOfTeamsOperatingModelCommandHandler>>());
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_RemovesTheCurrentModelAndReinstatesThePreviousOne()
    {
        // Arrange
        var team = _teamOfTeamsFaker
            .WithOperatingModel(new TeamOfTeamsOperatingModelFaker().WithTimeZone("UTC"), ActiveDate)
            .Generate();
        var first = team.OperatingModels.Single();
        var second = team.SetOperatingModel(ActiveDate.PlusMonths(6), "America/Chicago", EventActor.System, _dateTimeProvider.Now).Value;
        second.SetPrivate(m => m.Id, Guid.NewGuid());
        _dbContext.AddTeamOfTeams(team);

        // Act
        var result = await _handler.Handle(new DeleteTeamOfTeamsOperatingModelCommand(team.Id, second.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        team.OperatingModels.Should().ContainSingle().Which.Should().BeSameAs(first);
        first.IsCurrent.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_TheLastModel_ReturnsFailureWithoutSaving()
    {
        // Arrange
        var team = _teamOfTeamsFaker
            .WithOperatingModel(new TeamOfTeamsOperatingModelFaker(), ActiveDate)
            .Generate();
        _dbContext.AddTeamOfTeams(team);

        // Act
        var result = await _handler.Handle(new DeleteTeamOfTeamsOperatingModelCommand(team.Id, team.OperatingModels.Single().Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        team.OperatingModels.Should().ContainSingle();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_UnknownTeamOfTeams_ReturnsFailure()
    {
        // Arrange
        var teamId = Guid.NewGuid();

        // Act
        var result = await _handler.Handle(new DeleteTeamOfTeamsOperatingModelCommand(teamId, Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be($"Team of Teams with Id {teamId} not found.");
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
