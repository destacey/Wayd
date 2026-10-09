using FluentAssertions;
using NodaTime;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Work.Application.Iterations.Commands;
using Wayd.Work.Application.Tests.Infrastructure;
using static Wayd.Work.Application.Tests.Infrastructure.SprintLifecycleScenario;

namespace Wayd.Work.Application.Tests.Sut.Iterations.Commands;

public class SetSprintTeamDaysOffCommandHandlerTests : IDisposable
{
    private static readonly LocalDate Offsite = Sprint1Start.PlusDays(3);

    private readonly SprintLifecycleScenario _scenario = new();
    private readonly SetSprintTeamDaysOffCommandHandler _handler;

    public SetSprintTeamDaysOffCommandHandlerTests()
    {
        _handler = new SetSprintTeamDaysOffCommandHandler(
            _scenario.DbContext,
            _scenario.Dispatcher.Object,
            _scenario.CurrentUser.Object,
            _scenario.CurrentPrincipal.Object,
            _scenario.DateTimeProvider,
            Mock.Of<ILogger<SetSprintTeamDaysOffCommandHandler>>());
    }

    public void Dispose() => _scenario.Dispose();

    [Fact]
    public async Task Handle_ForATeamMember_SetsTheDaysOff()
    {
        // Act
        var result = await _handler.Handle(new SetSprintTeamDaysOffCommand(_scenario.Sprint1.Id, [Offsite]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.Sprint1.TeamDaysOff.Should().Equal(Offsite);
        _scenario.Sprint1.DomainEvents.OfType<SprintTeamDaysOffChangedEvent>().Should().ContainSingle();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ForSomeoneOutsideTheTeam_FailsWithoutSaving()
    {
        // Arrange
        _scenario.IsMember = false;

        // Act
        var result = await _handler.Handle(new SetSprintTeamDaysOffCommand(_scenario.Sprint1.Id, [Offsite]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _scenario.Sprint1.TeamDaysOff.Should().BeEmpty();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForAnAdministratorOutsideTheTeam_SetsTheDaysOff()
    {
        // Arrange
        _scenario.IsMember = false;
        _scenario.IsAdministrator = true;

        // Act
        var result = await _handler.Handle(new SetSprintTeamDaysOffCommand(_scenario.Sprint1.Id, [Offsite]), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.Sprint1.TeamDaysOff.Should().Equal(Offsite);
    }

    [Fact]
    public async Task Handle_WithADayOutsideTheSprint_FailsWithoutSaving()
    {
        // Act
        var result = await _handler.Handle(new SetSprintTeamDaysOffCommand(_scenario.Sprint1.Id, [Sprint2Start]), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
