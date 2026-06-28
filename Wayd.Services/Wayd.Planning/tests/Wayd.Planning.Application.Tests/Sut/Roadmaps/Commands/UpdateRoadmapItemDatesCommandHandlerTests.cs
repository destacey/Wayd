using Microsoft.Extensions.Logging;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Models;
using Wayd.Planning.Application.Roadmaps.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Interfaces.Roadmaps;
using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Planning.Domain.Tests.Data;
using Moq;
using NodaTime;
using OneOf;

namespace Wayd.Planning.Application.Tests.Sut.Roadmaps.Commands;

public class UpdateRoadmapItemDatesCommandHandlerTests : IDisposable
{
    private readonly FakePlanningDbContext _dbContext;
    private readonly Mock<ILogger<UpdateRoadmapItemDatesCommandHandler>> _mockLogger;
    private readonly Mock<ICurrentUser> _mockCurrentUser;
    private readonly Guid _currentEmployeeId = Guid.NewGuid();
    private readonly RoadmapFaker _faker = new();
    private readonly LocalDate _today = LocalDate.FromDateTime(DateTime.Today);

    public UpdateRoadmapItemDatesCommandHandlerTests()
    {
        _dbContext = new FakePlanningDbContext();
        _mockLogger = new Mock<ILogger<UpdateRoadmapItemDatesCommandHandler>>();
        _mockCurrentUser = new Mock<ICurrentUser>();
        _mockCurrentUser.Setup(u => u.GetEmployeeId()).Returns(_currentEmployeeId);
    }

    private UpdateRoadmapItemDatesCommandHandler CreateHandler() =>
        new(_dbContext, _mockCurrentUser.Object, _mockLogger.Object);

    private Roadmap CreateActiveRoadmap()
    {
        var fakeRoadmap = _faker.Generate();
        return Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [_currentEmployeeId]).Value;
    }

    [Fact]
    public async Task Handle_WhenChildGrowsBeyondParent_ShouldRollUpParentAndSave()
    {
        // Arrange - a parent activity with a child whose range starts inside the parent
        var roadmap = CreateActiveRoadmap();

        var parentActivity = new UpsertActivity(new LocalDateRange(_today, _today.PlusDays(10)));
        var parentResult = roadmap.CreateActivity(parentActivity, _currentEmployeeId);
        parentResult.IsSuccess.Should().BeTrue();

        var childActivity = new UpsertActivity(new LocalDateRange(_today, _today.PlusDays(10)), parentResult.Value.Id);
        var childResult = roadmap.CreateActivity(childActivity, _currentEmployeeId);
        childResult.IsSuccess.Should().BeTrue();

        _dbContext.AddRoadmap(roadmap);
        var handler = CreateHandler();

        // Widen the child well beyond the parent's end via the date-update command
        var newChildRange = new LocalDateRange(_today, _today.PlusDays(40));
        var dates = OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange>
            .FromT0(new UpsertActivityDateRange(newChildRange));
        var command = new UpdateRoadmapItemDatesCommand(roadmap.Id, childResult.Value.Id, dates);

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - the parent rolled up to contain the widened child, and the change was persisted
        result.IsSuccess.Should().BeTrue();
        parentResult.Value.DateRange.End.Should().Be(_today.PlusDays(40));
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WhenParentShrunkBehindChild_ShouldFailAndNotSave()
    {
        // Arrange - a parent activity with a child; the child grows the parent to [0, 30]
        var roadmap = CreateActiveRoadmap();

        var parentActivity = new UpsertActivity(new LocalDateRange(_today, _today.PlusDays(10)));
        var parentResult = roadmap.CreateActivity(parentActivity, _currentEmployeeId);
        parentResult.IsSuccess.Should().BeTrue();

        var childActivity = new UpsertActivity(
            new LocalDateRange(_today, _today.PlusDays(30)),
            parentResult.Value.Id);
        var childResult = roadmap.CreateActivity(childActivity, _currentEmployeeId);
        childResult.IsSuccess.Should().BeTrue();

        _dbContext.AddRoadmap(roadmap);
        var handler = CreateHandler();

        // Attempt to shrink the PARENT behind its child's end
        var dates = OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange>
            .FromT0(new UpsertActivityDateRange(new LocalDateRange(_today, _today.PlusDays(5))));
        var command = new UpdateRoadmapItemDatesCommand(roadmap.Id, parentResult.Value.Id, dates);

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert - rejected and nothing persisted
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    [Fact]
    public async Task Handle_WhenRoadmapNotFound_ShouldFailAndNotSave()
    {
        // Arrange
        var handler = CreateHandler();
        var dates = OneOf<IUpsertRoadmapActivityDateRange, IUpsertRoadmapMilestoneDate, IUpsertRoadmapTimeboxDateRange>
            .FromT0(new UpsertActivityDateRange(new LocalDateRange(_today, _today.PlusDays(5))));
        var command = new UpdateRoadmapItemDatesCommand(Guid.NewGuid(), Guid.NewGuid(), dates);

        // Act
        var result = await handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
        GC.SuppressFinalize(this);
    }

    private sealed record UpsertActivity(LocalDateRange DateRange, Guid? ParentId = null) : IUpsertRoadmapActivity
    {
        public string Name { get; init; } = "Activity";
        public string? Description { get; init; }
        public string? Color { get; init; }
    }

    private sealed record UpsertActivityDateRange(LocalDateRange DateRange) : IUpsertRoadmapActivityDateRange;
}
