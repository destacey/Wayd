using FluentAssertions;
using Wayd.DemandManagement.Application.IntakeRequests;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Queries;

public sealed class PreviewIntakeRequestScoreQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private PreviewIntakeRequestScoreQueryHandler CreateHandler() => new(_scenario.DbContext, _scenario.Principal.Object);

    [Fact]
    public async Task Handle_ForAnAgent_CalculatesWithoutRecording()
    {
        // Arrange — an agent previews with no scoring model claim
        var agent = _scenario.Employee();
        var model = _scenario.ScoringModel();
        var request = _scenario.Request(_scenario.ScoresWith(_scenario.Desk(agentIds: [agent.Id]), model), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new PreviewIntakeRequestScoreQuery(request.Id, IntakeScenario.Ratings(model, 6m, 3m)), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue();
        result.Value.PrimaryValue.Should().Be(2m);
        result.Value.Outputs.Select(o => (o.Token, o.Value)).Should().Equal(("CoD", 12m), ("Score", 2m));
        request.Scores.Should().BeEmpty();
        _scenario.DbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ForSomeoneOffTheDesk_Fails()
    {
        // Arrange
        var model = _scenario.ScoringModel();
        var request = _scenario.Request(_scenario.ScoresWith(_scenario.Desk(), model), _scenario.Employee());
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(
            new PreviewIntakeRequestScoreQuery(request.Id, IntakeScenario.Ratings(model, 6m, 3m)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequestAuthorization.NotAnAgentError);
    }

    [Fact]
    public async Task Handle_OnADeskWithNoModel_Fails()
    {
        // Arrange
        var agent = _scenario.Employee();
        var model = _scenario.ScoringModel();
        var request = _scenario.Request(_scenario.Desk(agentIds: [agent.Id]), _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(
            new PreviewIntakeRequestScoreQuery(request.Id, IntakeScenario.Ratings(model, 6m, 3m)), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be(IntakeRequest.DeskDoesNotScoreError);
    }
}
