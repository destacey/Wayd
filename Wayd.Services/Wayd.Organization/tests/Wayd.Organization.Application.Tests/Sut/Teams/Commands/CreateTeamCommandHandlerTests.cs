using Microsoft.Extensions.Logging;
using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.SystemSettings;
using Wayd.Common.Domain.Identity;
using Wayd.Common.Domain.Models.Organizations;
using Wayd.Common.Domain.Settings;
using Wayd.Organization.Application.Teams.Commands;
using Wayd.Organization.Application.Tests.Infrastructure;
using Wayd.Organization.Domain.Enums;
using Wayd.Tests.Shared;

namespace Wayd.Organization.Application.Tests.Sut.Teams.Commands;

public class CreateTeamCommandHandlerTests : IDisposable
{
    private readonly FakeOrganizationDbContext _dbContext = new();
    private readonly Mock<ISettings<SchedulingSettings>> _schedulingSettings = new();
    private readonly CreateTeamCommandHandler _handler;

    public CreateTeamCommandHandlerTests()
    {
        var clock = new TestingDateTimeProvider(new FakeClock(Instant.FromUtc(2026, 6, 2, 0, 0)));

        var currentUser = new Mock<ICurrentUser>();
        currentUser.Setup(u => u.GetUserId()).Returns(SystemUser.Id);

        _handler = new CreateTeamCommandHandler(
            _dbContext,
            clock,
            currentUser.Object,
            _schedulingSettings.Object,
            Mock.Of<ILogger<CreateTeamCommandHandler>>());
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_StartsTheOperatingModelFromTheSchedulingSettings()
    {
        // Arrange
        _schedulingSettings
            .Setup(s => s.Get(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SchedulingSettings { DefaultTimeZone = "America/Chicago", DefaultCommitmentGraceDays = 2 });

        var activeDate = new LocalDate(2026, 1, 1);
        var command = new CreateTeamCommand("Payments", new TeamCode("PAY"), null, activeDate);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var operatingModel = _dbContext.Teams.Single().OperatingModels.Should().ContainSingle().Subject;
        operatingModel.DateRange.Start.Should().Be(activeDate);
        operatingModel.Methodology.Should().Be(Methodology.Kanban);
        operatingModel.SizingMethod.Should().Be(SizingMethod.Count);
        operatingModel.TimeZone.Should().Be("America/Chicago");
        operatingModel.CommitmentGraceDays.Should().Be(2);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }
}
