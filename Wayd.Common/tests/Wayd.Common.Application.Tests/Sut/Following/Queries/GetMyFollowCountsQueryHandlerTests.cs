using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Following;
using Wayd.Common.Application.Following.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Activities;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Following;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Following.Queries;

public sealed class GetMyFollowCountsQueryHandlerTests
{
    private const string GadgetType = "test.follow-counts.gadget";
    private const string WidgetType = "test.follow-counts.widget";

    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Test Follow-Counts Gadget");
    private static readonly FollowOwnerDescriptor Widget = new(WidgetType, "Test Follow-Counts Widget");
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 24, 9, 0);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeRecordReadRule _gadgets = new(GadgetType);
    private readonly FakeRecordReadRule _widgets = new(WidgetType);
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _employeeId = Guid.NewGuid();

    public GetMyFollowCountsQueryHandlerTests()
    {
        FollowOwners.Register(Gadget, Widget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
    }

    private GetMyFollowCountsQueryHandler CreateHandler() =>
        new(_dbContext, _dbContext, [_widgets, _gadgets], _currentPrincipal.Object, new TestingDateTimeProvider(new FakeClock(Now)));

    private void SeedFollow(string ownerType, Guid ownerId, Guid? employeeId = null) =>
        _dbContext.Follows.Add(Follow.Create(ownerType, ownerId, employeeId ?? _employeeId, Now.Minus(Duration.FromDays(30))).Value);

    private int CountOf(Wayd.Common.Application.Following.Dtos.FollowCountsDto counts, FollowView view) =>
        counts.Views.Single(v => v.View == view).Count;

    [Fact]
    public async Task Handle_ShouldCountEachTypeAndEachView_ForReadableFollowsOnly()
    {
        // Arrange
        var redGadget = _gadgets.Add("G-1", "Sprocket", health: HealthStatus.Unhealthy);
        var closedWidget = _widgets.Add("W-1", "Lever", lifecycle: LifecycleCategory.Completed);
        var changedWidget = _widgets.Add("W-2", "Pulley");
        var hidden = _gadgets.Add("G-2", "Flange", health: HealthStatus.Unhealthy);
        SeedFollow(GadgetType, redGadget.Id);
        SeedFollow(WidgetType, closedWidget.Id);
        SeedFollow(WidgetType, changedWidget.Id);
        SeedFollow(GadgetType, hidden.Id);
        SeedFollow(GadgetType, _gadgets.Add("G-3", "Cog").Id, employeeId: Guid.NewGuid());
        _gadgets.Hide(hidden.Id);
        _dbContext.ActivityLogs.Add(new ActivityLogEntry(
            Guid.NewGuid(), "TestChangedEvent", ActivityCategory.Created, "Test", "Test", changedWidget.Id, EventActor.System, Now.Minus(Duration.FromDays(2)), 0, null, "{}"));

        // Act
        var counts = await CreateHandler().Handle(new GetMyFollowCountsQuery(), TestContext.Current.CancellationToken);

        // Assert
        counts.Total.Should().Be(3);
        counts.Types.Select(t => (t.OwnerType, t.DisplayName, t.Count)).Should().Equal(
            (GadgetType, "Test Follow-Counts Gadget", 1),
            (WidgetType, "Test Follow-Counts Widget", 2));
        CountOf(counts, FollowView.Unhealthy).Should().Be(1, "the hidden unhealthy gadget is not counted");
        CountOf(counts, FollowView.Closed).Should().Be(1);
        CountOf(counts, FollowView.ChangedRecently).Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutATypeTheCallerCannotRead()
    {
        // Arrange
        SeedFollow(GadgetType, _gadgets.Add("G-1", "Sprocket").Id);
        SeedFollow(WidgetType, _widgets.Add("W-1", "Lever").Id);
        _gadgets.CanView = false;

        // Act
        var counts = await CreateHandler().Handle(new GetMyFollowCountsQuery(), TestContext.Current.CancellationToken);

        // Assert
        counts.Total.Should().Be(1);
        counts.Types.Select(t => t.OwnerType).Should().Equal(WidgetType);
    }

    [Fact]
    public async Task Handle_ShouldListReadableTypesAtZero_WhenTheCallerHasNoEmployee()
    {
        // Arrange
        SeedFollow(GadgetType, _gadgets.Add("G-1", "Sprocket").Id);
        _widgets.CanView = false;
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var counts = await CreateHandler().Handle(new GetMyFollowCountsQuery(), TestContext.Current.CancellationToken);

        // Assert
        counts.Total.Should().Be(0);
        counts.Types.Should().ContainSingle().Which.Should().Match<Wayd.Common.Application.Following.Dtos.FollowTypeCountDto>(t => t.OwnerType == GadgetType && t.Count == 0);
        counts.Views.Should().OnlyContain(v => v.Count == 0);
        counts.Views.Select(v => v.View).Should().BeEquivalentTo(Enum.GetValues<FollowView>());
    }
}
