using FluentAssertions;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Tests.Data;
using Wayd.DemandManagement.Application.IntakeDesks.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeDesks.Queries;

public sealed class GetIntakeScoringModelChoicesQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    [Fact]
    public async Task Handle_ReturnsOnlyActiveModels()
    {
        // Arrange — a proposed model is still being built, and an archived one is being retired
        var active = _scenario.ScoringModel();
        var archived = _scenario.ScoringModel();
        archived.Archive(EventActor.System, _scenario.Clock.Now);
        _scenario.DbContext.AddScoringModels(new ScoringModelFaker().AsProposedWsjf());

        // Act
        var result = await new GetIntakeScoringModelChoicesQueryHandler(_scenario.DbContext)
            .Handle(new GetIntakeScoringModelChoicesQuery(), TestContext.Current.CancellationToken);

        // Assert
        var choice = result.Should().ContainSingle().Subject;
        choice.Id.Should().Be(active.Id);
        choice.Name.Should().Be(active.Name);
    }
}
