using Moq;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.Risks.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Planning.Application.Tests.Sut.Risks.Commands;

public sealed class CreateRiskCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Guid _currentEmployeeId = Guid.NewGuid();
    private readonly Guid _teamId = Guid.NewGuid();
    private readonly CreateRiskCommandHandler _handler;

    public CreateRiskCommandHandlerTests()
    {
        _dbContext.AddPlanningTeam(new PlanningTeamFaker(TeamType.Team).WithId(_teamId).Generate());
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_currentEmployeeId);
        _handler = new CreateRiskCommandHandler(
            _dbContext,
            new TestingDateTimeProvider(new FakeClock(Instant.FromUtc(2026, 1, 1, 0, 0))),
            principal.Object,
            Mock.Of<ICurrentUser>(u => u.GetUserId() == "user-1"));
    }

    public void Dispose() => _dbContext.Dispose();

    private CreateRiskCommand Command(Guid teamId, Guid? assigneeId = null) =>
        new("Vendor may slip", null, teamId, RiskCategory.Owned, RiskGrade.Low, RiskGrade.Low, assigneeId, null, null);

    [Fact]
    public async Task Handle_ShouldCreateTheRisk()
    {
        // Act
        var result = await _handler.Handle(Command(_teamId), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheTeamDoesNotExist()
    {
        // Act
        var result = await _handler.Handle(Command(Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("One or more teams were not found.");
        _dbContext.Risks.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_WhenTheAssigneeIsNotAnEmployee()
    {
        // Act
        var result = await _handler.Handle(Command(_teamId, Guid.NewGuid()), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("One or more employees were not found.");
        _dbContext.Risks.Should().BeEmpty();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }
}
