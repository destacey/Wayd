using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Following.Queries;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Following;

namespace Wayd.Common.Application.Tests.Sut.Following.Queries;

public sealed class GetMyFollowsQueryHandlerTests
{
    private const string GadgetType = "test.my-follows.gadget";
    private const string WidgetType = "test.my-follows.widget";

    // Display names chosen so the groups sort Gadget before Widget, whatever order they register in.
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Test My-Follows Gadget");
    private static readonly FollowOwnerDescriptor Widget = new(WidgetType, "Test My-Follows Widget");
    private static readonly Instant Start = Instant.FromUtc(2026, 10, 1, 9, 0);

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

    private GetMyFollowsQueryHandler CreateHandler() => new(_dbContext, [_widgets, _gadgets], _currentPrincipal.Object);

    private void SeedFollow(string ownerType, Guid ownerId, int daysAfterStart, Guid? employeeId = null) =>
        _dbContext.Follows.Add(Follow.Create(ownerType, ownerId, employeeId ?? _employeeId, Start.Plus(Duration.FromDays(daysAfterStart))).Value);

    [Fact]
    public async Task Handle_ShouldGroupByOwnerType_NewestFollowFirst()
    {
        // Arrange
        var older = _gadgets.Add("G-1", "Sprocket");
        var newer = _gadgets.Add("G-2", "Flange");
        var widget = _widgets.Add("W-1", "Lever", parentKey: "P-9", parentName: "Assembly");
        SeedFollow(GadgetType, older.Id, 1);
        SeedFollow(GadgetType, newer.Id, 3);
        SeedFollow(WidgetType, widget.Id, 2);

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        var groups = result.Value;
        groups.Select(g => g.OwnerType).Should().Equal(GadgetType, WidgetType);

        groups[0].DisplayName.Should().Be("Test My-Follows Gadget");
        groups[0].Follows.TotalCount.Should().Be(2);
        groups[0].Follows.Items.Select(i => i.Key).Should().Equal("G-2", "G-1");
        groups[0].Follows.Items[0].Name.Should().Be("Flange");
        groups[0].Follows.Items[0].FollowedOn.Should().Be(Start.Plus(Duration.FromDays(3)));

        var widgetItem = groups[1].Follows.Items.Should().ContainSingle().Subject;
        widgetItem.Id.Should().Be(widget.Id);
        widgetItem.OwnerType.Should().Be(WidgetType);
        widgetItem.ParentKey.Should().Be("P-9");
        widgetItem.ParentName.Should().Be("Assembly");
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
        var group = result.Value.Should().ContainSingle().Subject;
        group.Follows.TotalCount.Should().Be(1);
        group.Follows.Items.Should().ContainSingle().Which.Id.Should().Be(mine.Id);
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
        var group = result.Value.Should().ContainSingle().Subject;
        group.Follows.TotalCount.Should().Be(1);
        group.Follows.Items.Should().ContainSingle().Which.Id.Should().Be(readable.Id);
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
        result.Value.Select(g => g.OwnerType).Should().Equal(WidgetType);
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
        var group = result.Value.Should().ContainSingle().Subject;
        group.Follows.TotalCount.Should().Be(1);
        group.Follows.Items.Select(i => i.Id).Should().Equal(kept.Id);
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutATypeWithNothingFollowed()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        _widgets.Add("W-1", "Lever");
        SeedFollow(GadgetType, gadget.Id, 1);

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.Value.Select(g => g.OwnerType).Should().Equal(GadgetType);
        _widgets.DescribeCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldPageWithinAGroup()
    {
        // Arrange
        for (var day = 1; day <= 5; day++)
        {
            SeedFollow(GadgetType, _gadgets.Add($"G-{day}", $"Gadget {day}").Id, day);
        }

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(GadgetType, PageNumber: 2, PageSize: 2), TestContext.Current.CancellationToken);

        // Assert
        var page = result.Value.Should().ContainSingle().Subject.Follows;
        page.Items.Select(i => i.Key).Should().Equal("G-3", "G-2");
        page.TotalCount.Should().Be(5);
        page.PageNumber.Should().Be(2);
        page.PageSize.Should().Be(2);
        page.TotalPages.Should().Be(3);
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
        result.Value.Select(g => g.OwnerType).Should().Equal(WidgetType);
        _gadgets.DescribeCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_ShouldReturnNothing_WhenTheCallerHasNoEmployee()
    {
        // Arrange
        SeedFollow(GadgetType, _gadgets.Add("G-1", "Sprocket").Id, 1);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(new GetMyFollowsQuery(), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        result.Value.Should().BeEmpty();
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
