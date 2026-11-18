using FluentAssertions;
using Moq;
using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Work.Application.Iterations.Sprints;
using Wayd.Work.Application.Tests.Infrastructure;
using Wayd.Work.Domain;
using Wayd.Work.Domain.Tests.Data;
using Xunit;

namespace Wayd.Work.Application.Tests.Sut.Iterations.Sprints;

public sealed class SprintLinkOwnerTypeTests : IDisposable
{
    private readonly SprintLifecycleScenario _scenario = new();

    public SprintLinkOwnerTypeTests()
    {
        CanView(true);
    }

    private SprintLinkOwnerType CreateSut() =>
        new(_scenario.DbContext, _scenario.CurrentPrincipal.Object, _scenario.Dispatcher.Object, _scenario.DateTimeProvider);

    private void CanView(bool holds) =>
        _scenario.CurrentPrincipal
            .Setup(p => p.HoldsPermission(SprintLinkOwnerType.ViewPermission, It.IsAny<CancellationToken>()))
            .ReturnsAsync(holds);

    private Task<LinkOwnerAccess?> Resolve(Guid sprintId) =>
        CreateSut().Resolve(sprintId, TestContext.Current.CancellationToken);

    [Fact]
    public void OwnerType_ShouldBeFiledUnderTheIterationsActivityType()
    {
        // Act
        var ownerType = CreateSut().OwnerType;

        // Assert
        ownerType.Should().Be("work.sprint");
        WorkLinkOwners.Sprint.ActivityAggregateType.Should().Be("Iteration");
    }

    [Theory]
    [InlineData(true, true, false, true)]
    [InlineData(true, false, false, false)]
    [InlineData(false, true, false, false)]
    [InlineData(true, false, true, true)]
    [InlineData(false, false, true, false)]
    public async Task Resolve_ShouldLetTheSprintsTeamManage_WithTheUpdatePermission(
        bool canUpdate, bool isMember, bool isAdministrator, bool expected)
    {
        // Arrange
        _scenario.CanUpdate = canUpdate;
        _scenario.IsMember = isMember;
        _scenario.IsAdministrator = isAdministrator;

        // Act
        var access = await Resolve(_scenario.Sprint1.Id);

        // Assert
        access.Should().Be(new LinkOwnerAccess(expected));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Resolve_ShouldLetNoOneManage_ASprintWithNoTeam_AsNoOtherSprintChangeDoes(bool isAdministrator)
    {
        // Arrange
        var sprint = new IterationFaker().AsSprint().WithKey(9).WithTeamId(null).Generate();
        _scenario.DbContext.AddIterations([sprint]);
        _scenario.IsAdministrator = isAdministrator;

        // Act
        var access = await Resolve(sprint.Id);

        // Assert
        sprint.TeamId.Should().BeNull();
        access.Should().Be(new LinkOwnerAccess(false));
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_WhenTheCallerCannotViewIterations()
    {
        // Arrange
        CanView(false);

        // Act
        var access = await Resolve(_scenario.Sprint1.Id);

        // Assert
        access.Should().BeNull();
    }

    [Fact]
    public async Task Resolve_ShouldBeNull_ForAnIterationThatIsNotASprint()
    {
        // Arrange
        var iteration = new IterationFaker().WithType(IterationType.Iteration).WithKey(10).Generate();
        _scenario.DbContext.AddIterations([iteration]);

        // Act
        var access = await Resolve(iteration.Id);

        // Assert
        access.Should().BeNull();
    }

    public void Dispose() => _scenario.Dispose();
}
