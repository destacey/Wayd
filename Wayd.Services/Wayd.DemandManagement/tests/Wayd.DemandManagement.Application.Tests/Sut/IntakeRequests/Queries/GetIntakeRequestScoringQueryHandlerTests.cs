using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Queries;

public sealed class GetIntakeRequestScoringQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    public GetIntakeRequestScoringQueryHandlerTests() => MapsterTestConfiguration.Ensure();

    private GetIntakeRequestScoringQueryHandler CreateHandler() => new(_scenario.DbContext, _scenario.Principal.Object);

    [Fact]
    public async Task Handle_ForAnAgent_ReturnsTheModelAndEveryScoreNewestFirst()
    {
        // Arrange
        var agent = _scenario.Employee();
        var model = _scenario.ScoringModel();
        var request = _scenario.Request(_scenario.ScoresWith(_scenario.Desk(agentIds: [agent.Id]), model), _scenario.Employee());
        _scenario.Scored(request, agent, 2m, 4m);
        _scenario.Scored(request, agent, 8m, 4m);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestScoringQuery(request.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.ScoringModel!.Id.Should().Be(model.Id);
        result.ScoringModel.Criteria.Select(c => c.Token).Should().Equal("BV", "JS");
        result.ScoringModelArchived.Should().BeFalse();
        result.CanScore.Should().BeTrue();
        result.Scores.Select(s => (s.Sequence, s.PrimaryValue)).Should().Equal((2L, 2m), (1L, 0.5m));
        var current = result.Scores[0];
        current.ScoredBy.Id.Should().Be(agent.Id);
        current.ScoringModel.Name.Should().Be(model.Name);
        current.Ratings.Select(r => (r.CriterionToken, r.RatingValue)).Should().Equal(("BV", 8m), ("JS", 4m));
        current.Outputs.Select(o => (o.Token, o.Value, o.IsPrimary)).Should().Equal(("CoD", 16m, false), ("Score", 2m, true));
    }

    [Fact]
    public async Task Handle_ForTheRequester_ShowsTheScoreButCannotScore()
    {
        // Arrange — a decision is easier to accept with the reasoning attached
        var agent = _scenario.Employee();
        var requester = _scenario.Employee();
        var request = _scenario.Request(_scenario.ScoresWith(_scenario.Desk(IntakeDeskVisibility.Private, agentIds: [agent.Id]),
            _scenario.ScoringModel()), requester);
        _scenario.Scored(request, agent, 6m, 3m);
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestScoringQuery(request.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.Scores.Should().ContainSingle();
        result.CanScore.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_ForAViewHolderOnASummaryOnlyDesk_ReturnsNull()
    {
        // Arrange — a restricted desk restricts the number that ranks one ask above another
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.ScoresWith(_scenario.Desk(IntakeDeskVisibility.SummaryOnly, agentIds: [agent.Id]),
            _scenario.ScoringModel()), _scenario.Employee());
        _scenario.Scored(request, agent, 6m, 3m);
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestScoringQuery(request.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_OnADeskWithNoModel_ReturnsNoModelAndCannotScore()
    {
        // Arrange
        var agent = _scenario.Employee();
        var request = _scenario.Request(_scenario.Desk(agentIds: [agent.Id]), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestScoringQuery(request.Id), TestContext.Current.CancellationToken);

        // Assert
        result.Should().NotBeNull();
        result!.ScoringModel.Should().BeNull();
        result.CanScore.Should().BeFalse();
        result.Scores.Should().BeEmpty();
        result.DeskRank.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WithAnArchivedModel_SaysSo()
    {
        // Arrange
        var agent = _scenario.Employee();
        var model = _scenario.ScoringModel();
        var request = _scenario.Request(_scenario.ScoresWith(_scenario.Desk(agentIds: [agent.Id]), model), _scenario.Employee());
        model.Archive(EventActor.System, _scenario.Clock.Now);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestScoringQuery(request.Id), TestContext.Current.CancellationToken);

        // Assert
        result!.ScoringModelArchived.Should().BeTrue();
        result.CanScore.Should().BeTrue();
    }

    [Theory]
    [InlineData(StatusCategory.Done)]
    [InlineData(StatusCategory.Removed)]
    public async Task Handle_ForAClosedRequest_CannotScoreOrRank(StatusCategory category)
    {
        // Arrange — scoring follows the edit lock: frozen once the request is closed
        var agent = _scenario.Employee();
        var desk = _scenario.ScoresWith(_scenario.Desk(agentIds: [agent.Id]), _scenario.ScoringModel());
        var request = _scenario.RequestIn(desk, _scenario.Employee(), category);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestScoringQuery(request.Id), TestContext.Current.CancellationToken);

        // Assert
        result!.ScoringModel.Should().NotBeNull();
        result.CanScore.Should().BeFalse();
        result.DeskRank.Should().BeNull();
    }

    [Fact]
    public async Task Handle_RanksAmongTheOpenRequestsOnItsDeskScoredWithItsModel()
    {
        // Arrange
        var agent = _scenario.Employee();
        var model = _scenario.ScoringModel();
        var desk = _scenario.ScoresWith(_scenario.Desk(agentIds: [agent.Id]), model);
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.Scored(request, agent, 6m, 3m);                                        // 2
        _scenario.Scored(_scenario.Request(desk, _scenario.Employee()), agent, 9m, 3m);   // 3, ahead
        _scenario.Scored(_scenario.Request(desk, _scenario.Employee()), agent, 3m, 3m);   // 1, behind
        _scenario.Request(desk, _scenario.Employee());                                   // unscored, not ranked
        var otherDesk = _scenario.ScoresWith(_scenario.Desk(agentIds: [agent.Id]), model);
        _scenario.Scored(_scenario.Request(otherDesk, _scenario.Employee()), agent, 30m, 3m); // another desk
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestScoringQuery(request.Id), TestContext.Current.CancellationToken);

        // Assert
        result!.DeskRank.Should().NotBeNull();
        result.DeskRank!.Position.Should().Be(2);
        result.DeskRank.Of.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WhenTheCurrentScoreCameFromAnEarlierModel_DoesNotRank()
    {
        // Arrange — scores from different models do not rank together
        var agent = _scenario.Employee();
        var desk = _scenario.ScoresWith(_scenario.Desk(agentIds: [agent.Id]), _scenario.ScoringModel());
        var request = _scenario.Request(desk, _scenario.Employee());
        _scenario.Scored(request, agent, 6m, 3m);
        _scenario.ScoresWith(desk, _scenario.ScoringModel());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestScoringQuery(request.Id), TestContext.Current.CancellationToken);

        // Assert
        result!.Scores.Should().ContainSingle();
        result.DeskRank.Should().BeNull();
    }
}
