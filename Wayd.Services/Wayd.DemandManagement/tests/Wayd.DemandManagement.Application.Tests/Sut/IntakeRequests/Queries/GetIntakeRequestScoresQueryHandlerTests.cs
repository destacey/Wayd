using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Queries;

public sealed class GetIntakeRequestScoresQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private GetIntakeRequestScoresQueryHandler CreateHandler() => new(_scenario.DbContext, _scenario.Principal.Object);

    [Fact]
    public async Task Handle_ReturnsTheCurrentScoreOfEachScoredRequest()
    {
        // Arrange
        var agent = _scenario.Employee();
        var model = _scenario.ScoringModel();
        var desk = _scenario.ScoresWith(_scenario.Desk(agentIds: [agent.Id]), model);
        var scored = _scenario.Request(desk, _scenario.Employee());
        _scenario.Scored(scored, agent, 2m, 4m);
        _scenario.Scored(scored, agent, 9m, 3m);
        _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestScoresQuery(), TestContext.Current.CancellationToken);

        // Assert
        var score = result.Should().ContainSingle().Subject;
        score.RequestId.Should().Be(scored.Id);
        score.Value.Should().Be(3m);
        score.ScoringModel.Id.Should().Be(model.Id);
        score.ScoringModel.Key.Should().Be(model.Key);
        score.IsDeskModel.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_LeavesOutScoresTheReaderMayNotRead()
    {
        // Arrange — a View holder reads the summary of a summary-only desk's request, never its score
        var agent = _scenario.Employee();
        var model = _scenario.ScoringModel();
        _scenario.Scored(_scenario.Request(_scenario.ScoresWith(_scenario.Desk(IntakeDeskVisibility.SummaryOnly, agentIds: [agent.Id]), model),
            _scenario.Employee()), agent, 6m, 3m);
        var open = _scenario.Request(_scenario.ScoresWith(_scenario.Desk(agentIds: [agent.Id]), model), _scenario.Employee());
        _scenario.Scored(open, agent, 6m, 3m);
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestScoresQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(s => s.RequestId).Should().Equal(open.Id);
    }

    [Fact]
    public async Task Handle_FlagsAScoreFromAModelTheDeskNoLongerUses()
    {
        // Arrange
        var agent = _scenario.Employee();
        var desk = _scenario.ScoresWith(_scenario.Desk(agentIds: [agent.Id]), _scenario.ScoringModel());
        _scenario.Scored(_scenario.Request(desk, _scenario.Employee()), agent, 6m, 3m);
        _scenario.ScoresWith(desk, _scenario.ScoringModel());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestScoresQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.IsDeskModel.Should().BeFalse();
    }

    [Fact]
    public async Task Handle_AppliesTheListsFilters()
    {
        // Arrange
        var agent = _scenario.Employee();
        var model = _scenario.ScoringModel();
        var desk = _scenario.ScoresWith(_scenario.Desk(agentIds: [agent.Id]), model);
        var otherDesk = _scenario.ScoresWith(_scenario.Desk(agentIds: [agent.Id]), model);
        var open = _scenario.Request(desk, _scenario.Employee());
        _scenario.Scored(open, agent, 6m, 3m);
        var active = _scenario.RequestIn(desk, _scenario.Employee(), StatusCategory.Active);
        _scenario.Scored(active, agent, 6m, 3m);
        _scenario.Scored(_scenario.Request(otherDesk, _scenario.Employee()), agent, 6m, 3m);
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestScoresQuery(DeskId: desk.Id, OpenOnly: true),
            TestContext.Current.CancellationToken);

        // Assert
        result.Select(s => s.RequestId).Should().BeEquivalentTo([open.Id, active.Id]);
    }
}
