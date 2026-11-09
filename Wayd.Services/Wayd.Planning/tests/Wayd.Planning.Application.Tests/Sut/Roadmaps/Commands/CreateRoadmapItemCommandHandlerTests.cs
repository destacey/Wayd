using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using NodaTime;
using NodaTime.Testing;
using OneOf;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Events;
using Wayd.Common.Models;
using Wayd.Planning.Application.Roadmaps.Commands;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Interfaces.Roadmaps;
using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Planning.Domain.Tests.Data;
using Wayd.Tests.Shared;

namespace Wayd.Planning.Application.Tests.Sut.Roadmaps.Commands;

public sealed class CreateRoadmapItemCommandHandlerTests : IDisposable
{
    private static readonly EventActor Actor = EventActor.User("user-1");
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 1, 0, 0);
    private static readonly LocalDate Today = Now.InUtc().Date;

    private readonly FakePlanningDbContext _dbContext = new();
    private readonly Mock<ICurrentPrincipal> _mockCurrentPrincipal = new();
    private readonly Guid _currentEmployeeId = Guid.NewGuid();
    private readonly RoadmapFaker _faker = new();

    public CreateRoadmapItemCommandHandlerTests()
    {
        _mockCurrentPrincipal.Setup(u => u.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(_currentEmployeeId);
    }

    private CreateRoadmapItemCommandHandler CreateHandler() =>
        new(_dbContext, _mockCurrentPrincipal.Object, Mock.Of<ICurrentUser>(u => u.GetUserId() == "user-1"),
            new TestingDateTimeProvider(new FakeClock(Now)), NullLogger<CreateRoadmapItemCommandHandler>.Instance);

    private Roadmap RoadmapWithRootActivities(int count)
    {
        var fakeRoadmap = _faker.Generate();
        var roadmap = Roadmap.Create(fakeRoadmap.Name, fakeRoadmap.Description, fakeRoadmap.DateRange, fakeRoadmap.Visibility, [_currentEmployeeId], Actor, Now).Value;
        for (var i = 0; i < count; i++)
        {
            roadmap.CreateActivity(Activity(), new RoadmapActor(_currentEmployeeId, false), Actor, Now).IsSuccess.Should().BeTrue();
        }

        _dbContext.AddRoadmap(roadmap);
        return roadmap;
    }

    private static TestUpsertRoadmapActivity Activity() => new(new LocalDateRange(Today, Today.PlusDays(5)));

    private static CreateRoadmapItemCommand Command(Roadmap roadmap, IUpsertRoadmapActivity activity, int? order) =>
        new(roadmap.Id, OneOf<IUpsertRoadmapActivity, IUpsertRoadmapMilestone, IUpsertRoadmapTimebox>.FromT0(activity), order);

    [Fact]
    public async Task Handle_WithAnOrder_CreatesTheActivityAtThatPlace()
    {
        // Arrange
        var roadmap = RoadmapWithRootActivities(2);

        // Act
        var result = await CreateHandler().Handle(Command(roadmap, Activity(), order: 1), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        roadmap.Items.OfType<RoadmapActivity>().Single(a => a.Id == result.Value).Order.Should().Be(1);
        _dbContext.SaveChangesCallCount.Should().Be(1);
    }

    [Fact]
    public async Task Handle_WithoutAnOrder_CreatesTheActivityLast()
    {
        // Arrange
        var roadmap = RoadmapWithRootActivities(2);

        // Act
        var result = await CreateHandler().Handle(Command(roadmap, Activity(), order: null), TestContext.Current.CancellationToken);

        // Assert
        result.IsSuccess.Should().BeTrue(result.IsFailure ? result.Error : null);
        roadmap.Items.OfType<RoadmapActivity>().Single(a => a.Id == result.Value).Order.Should().Be(3);
    }

    [Fact]
    public async Task Handle_WithAnOrderPastTheEnd_FailsAndDoesNotSave()
    {
        // Arrange
        var roadmap = RoadmapWithRootActivities(2);

        // Act
        var result = await CreateHandler().Handle(Command(roadmap, Activity(), order: 5), TestContext.Current.CancellationToken);

        // Assert
        result.IsFailure.Should().BeTrue();
        _dbContext.SaveChangesCallCount.Should().Be(0);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }
}
