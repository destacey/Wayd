using FluentAssertions;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Queries;

public sealed class GetIntakeRequestEstimatesQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private GetIntakeRequestEstimatesQueryHandler CreateHandler() => new(_scenario.DbContext, _scenario.Principal.Object);

    [Fact]
    public async Task Handle_ReturnsEachEstimatedRequestsEstimateWithItsScaleAndPosition()
    {
        // Arrange
        var agent = _scenario.Employee();
        var scale = _scenario.EstimationScale("T-Shirt Sizes");
        var desk = _scenario.EstimatesOn(_scenario.Desk(agentIds: [agent.Id]), scale);
        var estimated = _scenario.Request(desk, _scenario.Employee());
        _scenario.Estimated(estimated, desk, "XL");
        _scenario.Request(desk, _scenario.Employee());
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestEstimatesQuery(), TestContext.Current.CancellationToken);

        // Assert
        var row = result.Should().ContainSingle().Subject;
        row.RequestId.Should().Be(estimated.Id);
        row.Estimate.Value.Should().Be("XL");
        row.Estimate.Position.Should().Be(3);
        row.Estimate.Scale.Id.Should().Be(scale.Id);
        row.Estimate.Scale.Name.Should().Be("T-Shirt Sizes");
        row.Estimate.Scale.VersionId.Should().Be(scale.CurrentVersion.Id);
        row.Estimate.Scale.IsCurrentVersion.Should().BeTrue();
    }

    [Fact]
    public async Task Handle_LeavesOutEstimatesTheReaderMayNotRead()
    {
        // Arrange — a View holder reads the summary of a summary-only desk's request, never its estimate
        var scale = _scenario.EstimationScale();
        var summaryOnly = _scenario.EstimatesOn(_scenario.Desk(IntakeDeskVisibility.SummaryOnly), scale);
        _scenario.Estimated(_scenario.Request(summaryOnly, _scenario.Employee()), summaryOnly, "M");
        var open = _scenario.EstimatesOn(_scenario.Desk(), scale);
        var visible = _scenario.Request(open, _scenario.Employee());
        _scenario.Estimated(visible, open, "S");
        _scenario.SignedInAs(_scenario.Employee(), IntakeScenario.View);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestEstimatesQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Select(e => e.RequestId).Should().Equal(visible.Id);
    }

    [Fact]
    public async Task Handle_ForTheRequester_ReturnsTheirOwnEstimateOnASummaryOnlyDesk()
    {
        // Arrange — the requester reads their request's substance wherever it is
        var requester = _scenario.Employee();
        var desk = _scenario.EstimatesOn(_scenario.Desk(IntakeDeskVisibility.SummaryOnly), _scenario.EstimationScale());
        var request = _scenario.Request(desk, requester);
        _scenario.Estimated(request, desk, "L");
        _scenario.SignedInAs(requester);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestEstimatesQuery(Mine: true), TestContext.Current.CancellationToken);

        // Assert
        result.Should().ContainSingle().Which.Estimate.Value.Should().Be("L");
    }

    [Fact]
    public async Task Handle_FlagsAnEstimateOnAnOlderVersion()
    {
        // Arrange
        var agent = _scenario.Employee();
        var scale = _scenario.EstimationScale();
        var desk = _scenario.EstimatesOn(_scenario.Desk(agentIds: [agent.Id]), scale);
        _scenario.Estimated(_scenario.Request(desk, _scenario.Employee()), desk, "M");
        scale.AddVersion(Wayd.DemandManagement.Domain.Tests.Data.DemandEstimationScaleFactory.Version(2));
        _scenario.SignedInAs(agent);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestEstimatesQuery(), TestContext.Current.CancellationToken);

        // Assert
        var scaleOfEstimate = result.Should().ContainSingle().Subject.Estimate.Scale;
        scaleOfEstimate.VersionNumber.Should().Be(1);
        scaleOfEstimate.IsCurrentVersion.Should().BeFalse();
    }
}
