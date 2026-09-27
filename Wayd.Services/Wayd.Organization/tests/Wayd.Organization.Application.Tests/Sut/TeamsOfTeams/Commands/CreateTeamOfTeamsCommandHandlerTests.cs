using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Application.TeamsOfTeams.Commands;
using Wayd.Tests.Shared;

namespace Wayd.Organization.Application.Tests.Sut.TeamsOfTeams.Commands;

public class CreateTeamOfTeamsCommandHandlerTests : IDisposable
{
    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly Mock<ISettings<SchedulingSettings>> _schedulingSettings = new();
    private readonly CreateTeamOfTeamsCommandHandler _handler;

    public CreateTeamOfTeamsCommandHandlerTests()
    {
        var clock = new TestingDateTimeProvider(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0)));

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

        _handler = new CreateTeamOfTeamsCommandHandler(
            _dbContext,
            clock,
            currentUser.Object,
            _schedulingSettings.Object,
            Mock.Of<ILogger<CreateTeamOfTeamsCommandHandler>>());
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_StartsTheOperatingModelFromTheDefaultTimeZone()
    {
        // Arrange
        _schedulingSettings
            .Setup(s => s.Get(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SchedulingSettings { DefaultTimeZone = "America/Chicago", DefaultCommitmentGraceDays = 2 });

        var activeDate = new LocalDate(2026, 1, 1);
        var command = new CreateTeamOfTeamsCommand("Payments ART", new TeamCode("ART"), null, activeDate);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var operatingModel = _dbContext.TeamOfTeams.Single().OperatingModels.Should().ContainSingle().Subject;
        operatingModel.DateRange.Start.Should().Be(activeDate);
        operatingModel.DateRange.End.Should().BeNull();
        operatingModel.TimeZone.Should().Be("America/Chicago");
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }
}
