using FluentAssertions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Enums.Organization;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Planning.Application.PlanningIntervals;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain;
using Wayd.Planning.Domain.Tests.Data;
using Xunit;

namespace Wayd.Planning.Application.Tests.Sut.PlanningIntervals;

public sealed class PlanningIntervalObjectiveLinkOwnerTypeTests : IDisposable
{
    private const string ViewPermission = "Permissions.PlanningIntervalObjectives.View";
    private const string ManagePermission = "Permissions.PlanningIntervalObjectives.Manage";

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();

    public PlanningIntervalObjectiveLinkOwnerTypeTests()
    {
        _currentPrincipal.Setup(p => p.HoldsPermission(ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);
    }

    public void Dispose() => _dbContext.Dispose();

    private PlanningIntervalObjectiveLinkOwnerType CreateSut() => new(_dbContext, _currentPrincipal.Object);

    private Guid Seed()
    {
        var objective = new PlanningIntervalObjectiveFaker(Guid.NewGuid(), new PlanningTeamFaker(TeamType.Team).Generate(), ObjectiveStatus.NotStarted, false).Generate();
        _dbContext.AddPlanningIntervalObjective(objective);
        return objective.Id;
    }

    [Fact]
    public void OwnerType_ShouldBeFiledUnderTheRecordsActivityType()
    {
        // Act
        var ownerType = CreateSut().OwnerType;

        // Assert
        ownerType.Should().Be("planning.planning-interval-objective");
        PlanningLinkOwners.PlanningIntervalObjective.ActivityAggregateType.Should().Be("PlanningIntervalObjective");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Resolve_ShouldLetAReaderManage_OnlyWithThePermissionThatEditsTheRecord(bool mayEdit)
    {
        // Arrange — every other permission is refused, so naming the wrong one fails here
        var id = Seed();
        _currentPrincipal.Setup(p => p.HasPermission(ManagePermission, It.IsAny<CancellationToken>())).ReturnsAsync(mayEdit);

        // Act
        var access = await CreateSut().Resolve(id, TestContext.Current.CancellationToken);

        // Assert
        access.Should().Be(new LinkOwnerAccess(mayEdit));
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WithoutThePermissionThatReadsTheRecord()
    {
        // Arrange
        var id = Seed();
        _currentPrincipal.Setup(p => p.HoldsPermission(ViewPermission, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        _currentPrincipal.Setup(p => p.HasPermission(ManagePermission, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        // Act
        var access = await CreateSut().Resolve(id, TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheRecordDoesNotExist()
    {
        // Arrange
        Seed();

        // Act
        var access = await CreateSut().Resolve(Guid.NewGuid(), TestContext.Current.CancellationToken);

        // Assert
        access.Should().BeNull();
    }
}
