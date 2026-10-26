using FluentAssertions;
using NodaTime;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Notifications.Queries;
using Wayd.Common.Application.Tests.Infrastructure;
using Wayd.Common.Domain.Following;
using Wayd.Common.Domain.Notifications;

namespace Wayd.Common.Application.Tests.Sut.Notifications.Queries;

public sealed class GetMyMutedRecordsQueryHandlerTests
{
    private const string GadgetType = "test.muted-records.gadget";
    private const string WidgetType = "test.muted-records.widget";
    private static readonly FollowOwnerDescriptor Gadget = new(GadgetType, "Gadget");
    private static readonly FollowOwnerDescriptor Widget = new(WidgetType, "Widget");
    private static readonly Instant Start = Instant.FromUtc(2026, 10, 1, 9, 0);

    private readonly FakeWaydDbContext _dbContext = new();
    private readonly FakeRecordReadRule _gadgets = new(GadgetType);
    private readonly FakeRecordReadRule _widgets = new(WidgetType);
    private readonly Mock<ICurrentPrincipal> _currentPrincipal = new();
    private readonly Guid _employeeId = Guid.NewGuid();

    public GetMyMutedRecordsQueryHandlerTests()
    {
        FollowOwners.Register(Gadget, Widget);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_employeeId);
    }

    private GetMyMutedRecordsQueryHandler CreateHandler() => new(_dbContext, [_gadgets, _widgets], _currentPrincipal.Object);

    private void Mute(string ownerType, Guid ownerId, int daysAfterStart, Guid? employeeId = null) =>
        _dbContext.AddRecordMute(RecordMute.Create(ownerType, ownerId, employeeId ?? _employeeId, Start.Plus(Duration.FromDays(daysAfterStart))).Value);

    [Fact]
    public async Task Handle_ShouldListTheCallersMutesAcrossTypes_MostRecentlyMutedFirst()
    {
        // Arrange
        var older = _gadgets.Add("G-1", "Sprocket");
        var newer = _widgets.Add("W-1", "Cog", parentKey: "PI-3", parentName: "Spring");
        Mute(GadgetType, older.Id, 1);
        Mute(WidgetType, newer.Id, 5);
        _dbContext.AddFollow(Follow.Create(GadgetType, older.Id, _employeeId, Start).Value);

        // Act
        var result = await CreateHandler().Handle(new GetMyMutedRecordsQuery(new PageRequest()), TestContext.Current.CancellationToken);

        // Assert
        result.TotalCount.Should().Be(2);
        result.Items.Select(i => (i.OwnerType, i.Key, i.IsFollowing, i.MutedOn)).Should().Equal(
            (WidgetType, "W-1", false, Start.Plus(Duration.FromDays(5))),
            (GadgetType, "G-1", true, Start.Plus(Duration.FromDays(1))));
        var widget = result.Items[0];
        widget.Id.Should().Be(newer.Id);
        widget.Name.Should().Be("Cog");
        widget.ParentKey.Should().Be("PI-3");
        widget.ParentName.Should().Be("Spring");
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutAndNotCount_MutesOfRecordsTheCallerCannotRead()
    {
        // Arrange
        var visible = _gadgets.Add("G-1", "Sprocket");
        var hidden = _gadgets.Add("G-2", "Flange");
        var widget = _widgets.Add("W-1", "Cog");
        _gadgets.Hide(hidden.Id);
        _widgets.CanView = false;
        Mute(GadgetType, visible.Id, 1);
        Mute(GadgetType, hidden.Id, 2);
        Mute(WidgetType, widget.Id, 3);

        // Act
        var result = await CreateHandler().Handle(new GetMyMutedRecordsQuery(new PageRequest()), TestContext.Current.CancellationToken);

        // Assert
        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle().Which.Id.Should().Be(visible.Id);
    }

    [Fact]
    public async Task Handle_ShouldLeaveOutOtherPeoplesMutes()
    {
        // Arrange
        var gadget = _gadgets.Add("G-1", "Sprocket");
        Mute(GadgetType, gadget.Id, 1, employeeId: Guid.NewGuid());

        // Act
        var result = await CreateHandler().Handle(new GetMyMutedRecordsQuery(new PageRequest()), TestContext.Current.CancellationToken);

        // Assert
        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_ShouldPage_KeepingTheTotal()
    {
        // Arrange
        for (var i = 1; i <= 5; i++)
        {
            Mute(GadgetType, _gadgets.Add($"G-{i}", $"Gadget {i}").Id, i);
        }

        // Act
        var result = await CreateHandler().Handle(new GetMyMutedRecordsQuery(new PageRequest(2, 2)), TestContext.Current.CancellationToken);

        // Assert
        result.TotalCount.Should().Be(5);
        result.Items.Select(i => i.Key).Should().Equal("G-3", "G-2");
    }

    [Fact]
    public async Task Handle_ShouldReturnAnEmptyPage_WhenTheCallerHasNoEmployee()
    {
        // Arrange
        Mute(GadgetType, _gadgets.Add("G-1", "Sprocket").Id, 1);
        _currentPrincipal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync((Guid?)null);

        // Act
        var result = await CreateHandler().Handle(new GetMyMutedRecordsQuery(new PageRequest()), TestContext.Current.CancellationToken);

        // Assert
        result.TotalCount.Should().Be(0);
        result.Items.Should().BeEmpty();
    }
}
