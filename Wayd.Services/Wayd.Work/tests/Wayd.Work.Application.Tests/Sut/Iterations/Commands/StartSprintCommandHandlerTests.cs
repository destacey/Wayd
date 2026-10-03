using FluentAssertions;
using NodaTime;
using Xunit;
using Microsoft.Extensions.Logging;
using Moq;
using Wayd.Common.Domain.Events.Planning.Iterations;
using Wayd.Work.Application.Iterations.Commands;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Tests.Infrastructure;
using static Wayd.Work.Application.Tests.Infrastructure.SprintLifecycleScenario;

namespace Wayd.Work.Application.Tests.Sut.Iterations.Commands;

public class StartSprintCommandHandlerTests : IDisposable
{
    private readonly SprintLifecycleScenario _scenario = new(sprint1Started: InChicago(Sprint1Start, 10));
    private readonly StartSprintCommandHandler _handler;

    public StartSprintCommandHandlerTests()
    {
        _handler = new StartSprintCommandHandler(
            _scenario.DbContext,
            _scenario.Dispatcher.Object,
            _scenario.SchedulingSettings.Object,
            _scenario.CurrentUser.Object,
            _scenario.CurrentPrincipal.Object,
            _scenario.DateTimeProvider,
            Mock.Of<ILogger<StartSprintCommandHandler>>());
    }

    public void Dispose() => _scenario.Dispose();

    [Fact]
    public async Task Handle_WhenAMemberConfirms_StartsTheSprintAndCompletesTheOpenOneAtTheSameInstant()
    {
        // Arrange
        var now = _scenario.DateTimeProvider.Now;

        // Act
        var result = await _handler.Handle(new StartSprintCommand(_scenario.Sprint2.Id, CompleteOpenSprintId: _scenario.Sprint1.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.Sprint2.Started.Should().Be(now);
        _scenario.Sprint1.Completed.Should().Be(now);
        _scenario.Sprint2.DomainEvents.OfType<SprintStartedEvent>().Should().ContainSingle()
            .Which.Actor.EmployeeId.Should().Be(_scenario.EmployeeId);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(2);
        _scenario.DbContext.UnitOfWorkCommitCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithAnEarlierStart_RecordsItAndCompletesTheOpenSprintAtThatMoment()
    {
        // Arrange — the team planned on Friday morning and records it that afternoon
        var startedAt = InChicago(Sprint2Start.PlusDays(-3), 10);

        // Act
        var result = await _handler.Handle(new StartSprintCommand(_scenario.Sprint2.Id, CompleteOpenSprintId: _scenario.Sprint1.Id, startedAt), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.Sprint2.Started.Should().Be(startedAt);
        _scenario.Sprint1.Completed.Should().Be(startedAt);
        _scenario.Sprint2.DomainEvents.OfType<SprintStartedEvent>().Should().ContainSingle()
            .Which.Timestamp.Should().Be(_scenario.DateTimeProvider.Now);
    }

    [Fact]
    public async Task Handle_WithAStartInTheFuture_Fails()
    {
        // Act
        var result = await _handler.Handle(
            new StartSprintCommand(_scenario.Sprint2.Id, CompleteOpenSprintId: _scenario.Sprint1.Id, _scenario.DateTimeProvider.Now.Plus(Duration.FromMinutes(5))),
            TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _scenario.Sprint2.Started.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheOpenSprintIsNotConfirmed_FailsAndSavesNothing()
    {
        // Act
        var result = await _handler.Handle(new StartSprintCommand(_scenario.Sprint2.Id, CompleteOpenSprintId: null), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _scenario.Sprint2.Started.Should().BeNull();
        _scenario.Sprint1.Completed.Should().BeNull();
        _scenario.Sprint1.DomainEvents.Should().BeEmpty();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheConfirmationNamesAnotherSprint_FailsAndCompletesNothing()
    {
        // Arrange — the dialog was confirmed for a sprint that is no longer the open one
        var confirmedFor = Guid.NewGuid();

        // Act
        var result = await _handler.Handle(new StartSprintCommand(_scenario.Sprint2.Id, CompleteOpenSprintId: confirmedFor), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain(_scenario.Sprint1.Name);
        _scenario.Sprint1.Completed.Should().BeNull();
        _scenario.Sprint2.Started.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheCallerIsNotAMember_Fails()
    {
        // Arrange
        _scenario.IsMember = false;

        // Act
        var result = await _handler.Handle(new StartSprintCommand(_scenario.Sprint2.Id, CompleteOpenSprintId: _scenario.Sprint1.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(SprintAuthorization.NotAMemberError);
        _scenario.Sprint2.Started.Should().BeNull();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenAnAdministratorIsNotAMember_StartsTheSprint()
    {
        // Arrange
        _scenario.IsMember = false;
        _scenario.IsAdministrator = true;

        // Act
        var result = await _handler.Handle(new StartSprintCommand(_scenario.Sprint2.Id, CompleteOpenSprintId: _scenario.Sprint1.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        _scenario.Sprint2.Started.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WithoutTheUpdatePermission_FailsEvenForAnAdministrator()
    {
        // Arrange
        _scenario.CanUpdate = false;
        _scenario.IsAdministrator = true;

        // Act
        var result = await _handler.Handle(new StartSprintCommand(_scenario.Sprint2.Id, CompleteOpenSprintId: _scenario.Sprint1.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenTheSprintDoesNotExist_Fails()
    {
        // Act
        var result = await _handler.Handle(new StartSprintCommand(Guid.NewGuid(), CompleteOpenSprintId: _scenario.Sprint1.Id), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Sprint not found.");
    }
}
