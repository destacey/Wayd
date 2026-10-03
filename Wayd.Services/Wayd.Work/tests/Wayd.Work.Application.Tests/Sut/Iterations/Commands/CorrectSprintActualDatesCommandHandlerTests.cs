using FluentAssertions;
using NodaTime;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Common.Domain.Models.Planning.Iterations;
using Wayd.Work.Application.Iterations.Commands;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Domain.Tests.Data;
using static Wayd.Work.Application.Tests.Infrastructure.SprintLifecycleScenario;

namespace Wayd.Work.Application.Tests.Sut.Iterations.Commands;

public class CorrectSprintActualDatesCommandHandlerTests : IDisposable
{
    private static readonly Instant Sprint1Started = InChicago(Sprint1Start, 10);

    private readonly SprintLifecycleScenario _scenario = new(sprint1Started: Sprint1Started);
    private readonly CorrectSprintActualDatesCommandHandler _handler;

    public CorrectSprintActualDatesCommandHandlerTests()
    {
        _scenario.Clock.Reset(InChicago(Sprint2Start.PlusDays(5), 9));

        _handler = new CorrectSprintActualDatesCommandHandler(
            _scenario.DbContext,
            _scenario.Dispatcher.Object,
            _scenario.SchedulingSettings.Object,
            _scenario.CurrentUser.Object,
            _scenario.CurrentPrincipal.Object,
            _scenario.DateTimeProvider,
            Mock.Of<ILogger<CorrectSprintActualDatesCommandHandler>>());
    }

    public void Dispose() => _scenario.Dispose();

    [Fact]
    public async Task Handle_ASingleSprint_RecordsTheDatesAndRaisesTheCorrection()
    {
        // Arrange
        var completed = InChicago(Sprint2Start.PlusDays(-3), 15);
        var command = new CorrectSprintActualDatesCommand([new(_scenario.Sprint1.Id, Sprint1Started, completed)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.Sprint1.Completed.Should().Be(completed);
        var correctedEvent = _scenario.Sprint1.DomainEvents.OfType<SprintActualDatesCorrectedEvent>().Should().ContainSingle().Subject;
        correctedEvent.Previous.Should().Be(new SprintActualDates(Sprint1Started, null));
        correctedEvent.Current.Should().Be(new SprintActualDates(Sprint1Started, completed));
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ClosingTheOpenSprintAndOpeningTheNext_SavesTheClosingFirst()
    {
        // Arrange — the team forgot to press Start on Friday, so sprint 1 stayed open
        var friday = InChicago(Sprint2Start.PlusDays(-3), 15);
        var command = new CorrectSprintActualDatesCommand(
        [
            new(_scenario.Sprint2.Id, friday, null),
            new(_scenario.Sprint1.Id, Sprint1Started, friday),
        ]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.Sprint1.Completed.Should().Be(friday);
        _scenario.Sprint2.Started.Should().Be(friday);
        _scenario.Sprint1.DomainEvents.OfType<SprintActualDatesCorrectedEvent>().Should().ContainSingle();
        _scenario.Sprint2.DomainEvents.OfType<SprintActualDatesCorrectedEvent>().Should().ContainSingle();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(2);
    }

    [Fact]
    public async Task Handle_WhenTheCorrectionWouldOverlap_FailsAndSavesNothing()
    {
        // Arrange — sprint 2 starts while sprint 1 stays open
        var command = new CorrectSprintActualDatesCommand([new(_scenario.Sprint2.Id, InChicago(Sprint2Start, 9), null)]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(_scenario.Sprint1.Name);
        _scenario.Sprint2.Started.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheCallerIsNotAMember_Fails()
    {
        // Arrange
        _scenario.IsMember = false;
        var command = new CorrectSprintActualDatesCommand([new(_scenario.Sprint1.Id, Sprint1Started, InChicago(Sprint2Start, 9))]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SprintAuthorization.NotAMemberError);
        _scenario.Sprint1.Completed.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenASprintBelongsToAnotherTeam_Fails()
    {
        // Arrange
        var otherSprint = new IterationFaker()
            .AsSprint()
            .WithKey(3)
            .WithTeam(new WorkTeamFaker(TeamType.Team).Generate())
            .WithDateRange(new IterationDateRange(Sprint2Start, Sprint2Start.PlusDays(13)))
            .Generate();
        _scenario.DbContext.AddIterations([otherSprint]);
        var command = new CorrectSprintActualDatesCommand(
        [
            new(_scenario.Sprint1.Id, Sprint1Started, InChicago(Sprint2Start, 9)),
            new(otherSprint.Id, InChicago(Sprint2Start, 9), null),
        ]);

        // Act
        var result = await _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _scenario.Sprint1.Completed.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }
}
