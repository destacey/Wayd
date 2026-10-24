using FluentAssertions;
using NodaTime;
using NodaTime.Testing;
using Wayd.Common.Application.Following;
using Wayd.Common.Application.Following.Queries;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Activities;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Following;
using Wayd.Tests.Shared;

namespace Wayd.Common.Application.Tests.Sut.Following.Queries;

public sealed class GetMyFollowsQueryHandlerTests
{
    private const string GadgetType = "test.my-follows.gadget";
    private const string WidgetType = "test.my-follows.widget";

    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Test My-Follows Gadget");
    private static readonly FollowOwnerDescriptor Widget = new(WidgetType, "Test My-Follows Widget");
    private static readonly Instant Start = Instant.FromUtc(2026, 10, 1, 9, 0);
    private static readonly Instant Now = Instant.FromUtc(2026, 10, 24, 9, 0);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeRecordReadRule _gadgets = new(GadgetType);
    private readonly FakeRecordReadRule _widgets = new(WidgetType);
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _employeeId = Guid.NewGuid();

    public GetMyFollowsQueryHandlerTests()
    {
        FollowOwners.Register(Gadget, Widget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
    }

    private GetMyFollowsQueryHandler CreateHandler() =>
        new(_dbContext, _dbContext, [_widgets, _gadgets], _currentPrincipal.Object, new TestingDateTimeProvider(new FakeClock(Now)));

    private void SeedFollow(string ownerType, Guid ownerId, int daysAfterStart, Guid? employeeId = null) =>
        _dbContext.Follows.Add(Follow.Create(ownerType, ownerId, employeeId ?? _employeeId, Start.Plus(Duration.FromDays(daysAfterStart))).Value);

    private void SeedActivity(Guid recordId, Instant timestamp) =>
        _dbContext.ActivityLogs.Add(new ActivityLogEntry(
            Guid.NewGuid(), "TestChangedEvent", ActivityCategory.Created, "Test", "Test", recordId, EventActor.System, timestamp, 0, null, "{}"));

    [Fact]
    public async Task Handle_ShouldListEveryTypeTogether_MostRecentlyChangedFirst_ThenThoseWithoutActivityByFollow()
    {
        // Arrange
        var quiet = _gadgets.Add("G-1", "Sprocket");
        var busy = _widgets.Add("W-1", "Lever");
        var older = _gadgets.Add("G-2", "Flange");
        var neverChanged = _widgets.Add("W-2", "Pulley");
        SeedFollow(GadgetType, quiet.Id, 1);
        SeedFollow(WidgetType, busy.Id, 2);
        SeedFollow(GadgetType, older.Id, 3);
        SeedFollow(WidgetType, neverChanged.Id, 4);
        SeedActivity(quiet.Id, Now.Minus(Duration.FromDays(20)));
        SeedActivity(busy.Id, Now.Minus(Duration.FromDays(30)));
        SeedActivity(busy.Id, Now.Minus(Duration.FromHours(1)));
        SeedActivity(older.Id, Now.Minus(Duration.FromDays(25)));

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.TotalCount.Should().Be(4);
        result.Value.Items.Select(i => i.Key).Should().Equal("W-1", "G-1", "G-2", "W-2");
        result.Value.Items[0].LastChangedOn.Should().Be(Now.Minus(Duration.FromHours(1)));
        result.Value.Items[3].LastChangedOn.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldOrderByFollowDate_WhenAsked()
    {
        // Arrange
        var first = _gadgets.Add("G-1", "Sprocket");
        var second = _widgets.Add("W-1", "Lever");
        SeedFollow(GadgetType, first.Id, 1);
        SeedFollow(WidgetType, second.Id, 2);
        SeedActivity(first.Id, Now);

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(Sort: FollowSort.Followed), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Items.Select(i => i.Key).Should().Equal("W-1", "G-1");
    }

    [Fact]
    public async Task Handle_ShouldCountTheRecordsOwnAndRelatedActivity_ButNotAnotherTypeSharingItsId()
    {
        // Arrange
        var shadowed = _gadgets.Add("G-1", "Sprocket");
        var related = _widgets.Add("W-1", "Lever");
        SeedFollow(GadgetType, shadowed.Id, 1);
        SeedFollow(WidgetType, related.Id, 2);
        _dbContext.ActivityLogs.Add(new ActivityLogEntry(
            Guid.NewGuid(), "OtherChangedEvent", ActivityCategory.Updated, "Other", "Other", shadowed.Id, EventActor.System, Now, 0, null, "{}"));
        _dbContext.ActivityLogs.Add(new ActivityLogEntry(
            Guid.NewGuid(), "OtherChangedEvent", ActivityCategory.Updated, "Other", "Other", Guid.NewGuid(), EventActor.System,
            Now.Minus(Duration.FromHours(1)), 0, null, "{}",
            relatedAggregates: [new AggregateReference(FakeRecordReadRule.ActivityType, related.Id)]));

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Items.Select(i => i.Key).Should().Equal("W-1", "G-1");
        result.Value.Items[0].LastChangedOn.Should().Be(Now.Minus(Duration.FromHours(1)));
        result.Value.Items[1].LastChangedOn.Should().BeNull();
    }

    [Fact]
    public async Task Handle_ShouldListRecordsWithoutActivityFirst_ThenLeastRecentlyChanged_WhenAscending()
    {
        // Arrange
        var quiet = _gadgets.Add("G-1", "Sprocket");
        var busy = _widgets.Add("W-1", "Lever");
        var neverChanged = _widgets.Add("W-2", "Pulley");
        SeedFollow(GadgetType, quiet.Id, 1);
        SeedFollow(WidgetType, busy.Id, 2);
        SeedFollow(WidgetType, neverChanged.Id, 3);
        SeedActivity(quiet.Id, Now.Minus(Duration.FromDays(20)));
        SeedActivity(busy.Id, Now.Minus(Duration.FromHours(1)));

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(Direction: SortDirection.Ascending), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Items.Select(i => i.Key).Should().Equal("W-2", "G-1", "W-1");
    }

    [Fact]
    public async Task Handle_ShouldListEarliestFollowFirst_WhenSortedByFollowDateAscending()
    {
        // Arrange
        var first = _gadgets.Add("G-1", "Sprocket");
        var second = _widgets.Add("W-1", "Lever");
        SeedFollow(GadgetType, first.Id, 1);
        SeedFollow(WidgetType, second.Id, 2);

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(Sort: FollowSort.Followed, Direction: SortDirection.Ascending), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Items.Select(i => i.Key).Should().Equal("G-1", "W-1");
    }

    [Fact]
    public async Task Handle_ShouldDescribeEachRecordAsItsOwnerTypeDoes()
    {
        // Arrange
        var widget = _widgets.Add("W-1", "Lever", parentKey: "P-9", parentName: "Assembly", health: HealthStatus.AtRisk, lifecycle: LifecycleCategory.NotStarted);
        SeedFollow(WidgetType, widget.Id, 2);

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(), TestContext.Current.CancellationToken);

        // Assert
        var item = result.Value.Items.Should().ContainSingle().Subject;
        item.OwnerType.Should().Be(WidgetType);
        item.Id.Should().Be(widget.Id);
        item.Name.Should().Be("Lever");
        item.ParentKey.Should().Be("P-9");
        item.ParentName.Should().Be("Assembly");
        item.StatusName.Should().Be("NotStarted");
        item.Lifecycle.Should().Be(LifecycleCategory.NotStarted);
        item.Health.Should().Be(HealthStatus.AtRisk);
        item.FollowedOn.Should().Be(Start.Plus(Duration.FromDays(2)));
    }

    [Fact]
    public async Task Handle_ShouldReturnOnlyTheCallersFollows()
    {
        // Arrange
        var mine = _gadgets.Add("G-1", "Sprocket");
        var theirs = _gadgets.Add("G-2", "Flange");
        SeedFollow(GadgetType, mine.Id, 1);
        SeedFollow(GadgetType, theirs.Id, 2, employeeId: Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle().Which.Id.Should().Be(mine.Id);
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutRecordsTheCallerCanNoLongerRead_FromTheItemsAndTheTotal()
    {
        // Arrange
        var readable = _gadgets.Add("G-1", "Sprocket");
        var hidden = _gadgets.Add("G-2", "Flange");
        SeedFollow(GadgetType, readable.Id, 1);
        SeedFollow(GadgetType, hidden.Id, 2);
        _gadgets.Hide(hidden.Id);

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(), TestContext.Current.CancellationToken);

        // Assert
        // The total counting the hidden record would tell the caller one exists that they cannot see.
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Should().ContainSingle().Which.Id.Should().Be(readable.Id);
        _gadgets.LastDescribedIds.Should().Equal(readable.Id);
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutAWholeType_WhenTheCallerCannotReadIt()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        var widget = _widgets.Add("W-1", "Lever");
        SeedFollow(GadgetType, gadget.Id, 1);
        SeedFollow(WidgetType, widget.Id, 2);
        _gadgets.CanView = false;

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Select(i => i.OwnerType).Should().Equal(WidgetType);
        _gadgets.DescribeCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutFollowsOfDeletedRecords()
    {
        // Arrange
        var kept = _gadgets.Add("G-1", "Sprocket");
        var deleted = _gadgets.Add("G-2", "Flange");
        SeedFollow(GadgetType, kept.Id, 1);
        SeedFollow(GadgetType, deleted.Id, 2);
        _gadgets.Delete(deleted.Id);

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Select(i => i.Id).Should().Equal(kept.Id);
    }

    [Fact]
    public async Task Handle_ShouldPage()
    {
        // Arrange
        for (var day = 1; day <= 5; day++)
        {
            SeedFollow(GadgetType, _gadgets.Add($"G-{day}", $"Gadget {day}").Id, day);
        }

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(Sort: FollowSort.Followed, PageNumber: 2, PageSize: 2), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Items.Select(i => i.Key).Should().Equal("G-3", "G-2");
        result.Value.TotalCount.Should().Be(5);
        result.Value.PageNumber.Should().Be(2);
        result.Value.PageSize.Should().Be(2);
        result.Value.TotalPages.Should().Be(3);
    }

    [Fact]
    public async Task Handle_ShouldReturnOnlyTheRequestedOwnerType()
    {
        // Arrange
        SeedFollow(GadgetType, _gadgets.Add("G-1", "Sprocket").Id, 1);
        SeedFollow(WidgetType, _widgets.Add("W-1", "Lever").Id, 2);

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(WidgetType.ToUpperInvariant()), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Items.Select(i => i.OwnerType).Should().Equal(WidgetType);
        _gadgets.DescribeCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldNarrowToUnhealthyRecords()
    {
        // Arrange
        var red = _gadgets.Add("G-1", "Sprocket", health: HealthStatus.Unhealthy);
        SeedFollow(GadgetType, red.Id, 1);
        SeedFollow(GadgetType, _gadgets.Add("G-2", "Flange", health: HealthStatus.AtRisk).Id, 2);
        SeedFollow(WidgetType, _widgets.Add("W-1", "Lever").Id, 3);

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(View: FollowView.Unhealthy), TestContext.Current.CancellationToken);

        // Assert
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Select(i => i.Id).Should().Equal(red.Id);
    }

    [Fact]
    public async Task Handle_ShouldNarrowToClosedRecords_WhetherCompletedOrCanceled()
    {
        // Arrange
        var completed = _gadgets.Add("G-1", "Sprocket", lifecycle: LifecycleCategory.Completed);
        var canceled = _widgets.Add("W-1", "Lever", lifecycle: LifecycleCategory.Canceled);
        SeedFollow(GadgetType, completed.Id, 1);
        SeedFollow(WidgetType, canceled.Id, 2);
        SeedFollow(GadgetType, _gadgets.Add("G-2", "Flange", lifecycle: LifecycleCategory.Active).Id, 3);

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(View: FollowView.Closed, Sort: FollowSort.Followed), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Items.Select(i => i.Id).Should().Equal(canceled.Id, completed.Id);
    }

    [Fact]
    public async Task Handle_ShouldNarrowToRecordsChangedInTheLastSevenDays()
    {
        // Arrange
        var recent = _gadgets.Add("G-1", "Sprocket");
        var edge = _gadgets.Add("G-2", "Flange");
        var stale = _gadgets.Add("G-3", "Pulley");
        SeedFollow(GadgetType, recent.Id, 1);
        SeedFollow(GadgetType, edge.Id, 2);
        SeedFollow(GadgetType, stale.Id, 3);
        SeedFollow(GadgetType, _gadgets.Add("G-4", "Cog").Id, 4);
        SeedActivity(recent.Id, Now.Minus(Duration.FromDays(1)));
        SeedActivity(edge.Id, Now.Minus(Duration.FromDays(7)));
        SeedActivity(stale.Id, Now.Minus(Duration.FromDays(7)).Minus(Duration.FromSeconds(1)));

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(View: FollowView.ChangedRecently), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Items.Select(i => i.Id).Should().Equal(recent.Id, edge.Id);
    }

    [Fact]
    public async Task Handle_ShouldSearchThroughEachReadRule_WithTheSearchTrimmed()
    {
        // Arrange
        SeedFollow(GadgetType, _gadgets.Add("G-1", "Sprocket").Id, 1);
        var flange = _gadgets.Add("G-2", "Flange");
        SeedFollow(GadgetType, flange.Id, 2);

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(Search: "  flan "), TestContext.Current.CancellationToken);

        // Assert
        _gadgets.LastSearch.Should().Be("flan");
        result.Value.TotalCount.Should().Be(1);
        result.Value.Items.Select(i => i.Id).Should().Equal(flange.Id);
    }

    [Fact]
    public async Task Handle_ShouldTreatABlankSearchAsNone()
    {
        // Arrange
        SeedFollow(GadgetType, _gadgets.Add("G-1", "Sprocket").Id, 1);

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(Search: "   "), TestContext.Current.CancellationToken);

        // Assert
        _gadgets.LastSearch.Should().BeNull();
        result.Value.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_ShouldReturnAnEmptyPage_WhenTheCallerHasNoEmployee()
    {
        // Arrange
        SeedFollow(GadgetType, _gadgets.Add("G-1", "Sprocket").Id, 1);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Items.Should().BeEmpty();
        result.Value.TotalCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldFail_ForAnOwnerTypeThatCannotBeFollowed()
    {
        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery("test.my-follows.unknown"), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Contain("is not a kind of record that can be followed");
    }
}
