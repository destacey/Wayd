using FluentAssertions;
using Moq;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.DemandManagement.Application.IntakeRequests.Queries;
using Wayd.DemandManagement.Application.Tests.Infrastructure;
using Wayd.DemandManagement.Domain.Tests.Data;

namespace Wayd.DemandManagement.Application.Tests.Sut.IntakeRequests.Queries;

public sealed class GetIntakeRequestsQueryHandlerTests
{
    private readonly IntakeScenario _scenario = new();

    private GetIntakeRequestsQueryHandler CreateHandler() => new(_scenario.DbContext, _scenario.Principal.Object);

    [Fact]
    public async Task Handle_Mine_IncludesWhatTheCallerSubmittedForSomeoneElse()
    {
        // Arrange
        var caller = _scenario.Employee();
        var someoneElse = _scenario.Employee();
        var desk = _scenario.Desk(IntakeDeskVisibility.SummaryOnly);
        var onBehalf = new IntakeRequestFaker()
            .WithDeskId(desk.Id)
            .WithChannelId(_scenario.Channel().Id)
            .WithRequesterId(someoneElse.Id)
            .WithSubmittedById(caller.Id)
            .Generate();
        _scenario.DbContext.AddRequests(onBehalf);
        var requested = _scenario.Request(desk, caller);
        _scenario.Request(desk, someoneElse);
        _scenario.SignedInAs(caller);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestsQuery(Mine: true), TestContext.Current.CancellationToken);

        // Assert
        result.Select(r => r.Id).Should().BeEquivalentTo([onBehalf.Id, requested.Id]);
    }

    [Fact]
    public async Task Handle_Mine_LeavesOutRequestsTheCallerOnlyParticipatesIn()
    {
        // Arrange
        var caller = _scenario.Employee();
        _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Private), _scenario.Employee(), caller.Id);
        _scenario.SignedInAs(caller);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestsQuery(Mine: true), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_MineWithNoLinkedEmployee_ReturnsNothing()
    {
        // Arrange
        _scenario.Request(_scenario.Desk(IntakeDeskVisibility.Open), _scenario.Employee());
        _scenario.Principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(new GetIntakeRequestsQuery(Mine: true), TestContext.Current.CancellationToken);

        // Assert
        result.Should().BeEmpty();
    }
}
