using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.Common.Domain.Tests.Data;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Commands;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Commands;

public sealed class RecordIntakeRequestScoreCommandHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private RecordIntakeRequestScoreCommandHandler CreateHandler() =>
        new(_scenario.DbContext, _scenario.Principal.Object, _scenario.User.Object, _scenario.Clock);

    [Fact]
    public async Task Handle_ByAnAgentHoldingNoClaim_RecordsTheScore()
    {
        // Arrange — membership is the grant, so the agent needs no permission beside it
        var agent = _scenario.Employee();
        var model = _scenario.ScoringModel();
        var desk = _scenario.ScoresWith(_scenario.Desk(agentIds: [agent.Id]), model);
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new RecordIntakeRequestScoreCommand(request.Id, IntakeScenario.Ratings(model, 6m, 3m)), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var score = request.Scores.Should().ContainSingle().Subject;
        result.Value.Should().Be(score.Id);
        score.PrimaryValue.Should().Be(2m);
        score.ScoredById.Should().Be(agent.Id);
        request.CurrentScore!.Value.Should().Be(2m);
        request.DomainEvents.OfType<IntakeRequestScoreRecordedEvent>().Should().ContainSingle();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_OnAScale_UsesTheChosenLevelsValueAndLabel()
    {
        // Arrange
        var agent = _scenario.Employee();
        var model = new ScoringModelFaker().AsActiveWsjf();
        _scenario.DbContext.AddScoringModels(model);
        var desk = _scenario.ScoresWith(_scenario.Desk(agentIds: [agent.Id]), model);
        var request = _scenario.Request(desk, _scenario.Employee());
        var levels = model.Scales.Single().Levels;
        var ratings = model.Criteria
            .Select(c => new IntakeRequestCriterionRatingInput(c.Id, null, levels.Single(l => l.Label == (c.Token == "JS" ? "Low" : "High")).Id))
            .ToList();
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new RecordIntakeRequestScoreCommand(request.Id, ratings), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        var score = request.Scores.Single();
        score.PrimaryValue.Should().Be(24m);
        score.Ratings.Single(r => r.CriterionToken == "JS").RatingLevelLabel.Should().Be("Low");
    }

    [Fact]
    public async Task Handle_WithALevelFromAnotherScale_FailsAndSavesNothing()
    {
        // Arrange
        var agent = _scenario.Employee();
        var model = new ScoringModelFaker().AsActiveWsjf();
        _scenario.DbContext.AddScoringModels(model);
        var desk = _scenario.ScoresWith(_scenario.Desk(agentIds: [agent.Id]), model);
        var request = _scenario.Request(desk, _scenario.Employee());
        var ratings = model.Criteria.Select(c => new IntakeRequestCriterionRatingInput(c.Id, 8m, Guid.NewGuid())).ToList();
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new RecordIntakeRequestScoreCommand(request.Id, ratings), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("choosing a level from its scale");
        request.Scores.Should().BeEmpty();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WithACriterionUnrated_Fails()
    {
        // Arrange
        var agent = _scenario.Employee();
        var model = _scenario.ScoringModel();
        var desk = _scenario.ScoresWith(_scenario.Desk(agentIds: [agent.Id]), model);
        var request = _scenario.Request(desk, _scenario.Employee());
        var ratings = IntakeScenario.Ratings(model, 6m, 3m).Take(1).ToList();
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new RecordIntakeRequestScoreCommand(request.Id, ratings), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("has not been rated");
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(250_000)]
    [InlineData(-100_000)]
    [InlineData(1.23456)]
    public async Task Handle_WithANumberAScoreCannotHold_FailsAndSavesNothing(decimal value)
    {
        // Arrange — a rating is stored to four decimal places with five digits before the point
        var agent = _scenario.Employee();
        var model = _scenario.ScoringModel();
        var request = _scenario.Request(_scenario.ScoresWith(_scenario.Desk(agentIds: [agent.Id]), model), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new RecordIntakeRequestScoreCommand(request.Id, IntakeScenario.Ratings(model, value, 3m)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("Business Value").And.Contain("four decimal places");
        request.Scores.Should().BeEmpty();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ByAViewHolderNotOnTheDesk_FailsAndSavesNothing()
    {
        // Arrange — View browses the request but scores nothing
        var model = _scenario.ScoringModel();
        var request = _scenario.Request(_scenario.ScoresWith(_scenario.Desk(), model), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(
            new RecordIntakeRequestScoreCommand(request.Id, IntakeScenario.Ratings(model, 6m, 3m)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAnAgentError);
        request.Scores.Should().BeEmpty();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ByTheRequester_Fails()
    {
        // Arrange — the requester reads the score but never sets it
        var requester = _scenario.Employee();
        var model = _scenario.ScoringModel();
        var request = _scenario.Request(_scenario.ScoresWith(_scenario.Desk(), model), requester);
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(
            new RecordIntakeRequestScoreCommand(request.Id, IntakeScenario.Ratings(model, 6m, 3m)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAnAgentError);
    }

    [Fact]
    public async Task Handle_OnAPrivateDeskTheCallerCannotSee_ReportsNotFound()
    {
        // Arrange
        var model = _scenario.ScoringModel();
        var request = _scenario.Request(_scenario.ScoresWith(_scenario.Desk(IntakeDeskVisibility.Private), model), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(
            new RecordIntakeRequestScoreCommand(request.Id, IntakeScenario.Ratings(model, 6m, 3m)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAccess.NotFoundError);
    }

    [Fact]
    public async Task Handle_OnADeskWithNoModel_Fails()
    {
        // Arrange — a desk that does not score is a normal state, not a broken one
        var agent = _scenario.Employee();
        var model = _scenario.ScoringModel();
        var request = _scenario.Request(_scenario.Desk(agentIds: [agent.Id]), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new RecordIntakeRequestScoreCommand(request.Id, IntakeScenario.Ratings(model, 6m, 3m)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.DeskDoesNotScoreError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Theory]
    [InlineData(StatusCategory.Done)]
    [InlineData(StatusCategory.Removed)]
    public async Task Handle_OnAClosedRequest_Fails(StatusCategory category)
    {
        // Arrange
        var agent = _scenario.Employee();
        var model = _scenario.ScoringModel();
        var request = _scenario.RequestIn(_scenario.ScoresWith(_scenario.Desk(agentIds: [agent.Id]), model), _scenario.Employee(), category);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new RecordIntakeRequestScoreCommand(request.Id, IntakeScenario.Ratings(model, 6m, 3m)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.ClosedScoreError);
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenAnotherScoreTakesTheSameNumber_ReportsIt()
    {
        // Arrange
        var agent = _scenario.Employee();
        var model = _scenario.ScoringModel();
        var request = _scenario.Request(_scenario.ScoresWith(_scenario.Desk(agentIds: [agent.Id]), model), _scenario.Employee());
        _scenario.SignedInAs(agent);
        _scenario.DbContext.FailNextSaveWith = new DbUpdateException("Duplicate key on (RequestId, Sequence).");

        // Act
        var result = await CreateHandler().Handle(
            new RecordIntakeRequestScoreCommand(request.Id, IntakeScenario.Ratings(model, 6m, 3m)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(RecordIntakeRequestScoreCommandHandler.ScoredMeanwhileError);
    }
}
