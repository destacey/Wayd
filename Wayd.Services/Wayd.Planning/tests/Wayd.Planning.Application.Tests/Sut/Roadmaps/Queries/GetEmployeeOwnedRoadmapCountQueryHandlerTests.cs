using NodaTime;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Models;
using Wayd.Planning.Application.Roadmaps.Queries;
using Wayd.Planning.Application.Tests.Infrastructure;
using Wayd.Planning.Domain.Models.Roadmaps;

namespace Wayd.Planning.Application.Tests.Sut.Roadmaps.Queries;

public sealed class GetEmployeeOwnedRoadmapCountQueryHandlerTests
{
    private static readonly EventActor Actor = EventActor.User("user-1");
    private static readonly Instant Now = Instant.FromUtc(2026, 1, 1, 0, 0);
    private static readonly LocalDateRange AnyDateRange = new(new LocalDate(2026, 1, 1), new LocalDate(2026, 12, 31));

    private readonly FakePlanningDbContext _dbContext = new();

    private Roadmap AddRoadmap(Visibility visibility, params Guid[] ownerIds)
    {
        var roadmap = Roadmap.Create("Roadmap", null, AnyDateRange, visibility, ownerIds, Actor, Now).Value;
        _dbContext.AddRoadmap(roadmap);
        return roadmap;
    }

    [Fact]
    public async Task Handle_CountsOnlyActiveRoadmapsTheEmployeeOwns()
    {
        // Arrange
        var employeeId = Guid.NewGuid();
        AddRoadmap(Visibility.Private, employeeId);
        AddRoadmap(Visibility.Public, employeeId, Guid.NewGuid());
        var archived = AddRoadmap(Visibility.Public, employeeId);
        archived.Archive(new RoadmapActor(employeeId, false), Actor, Now).IsSuccess.Should().BeTrue();
        AddRoadmap(Visibility.Public, Guid.NewGuid());
        var sut = new GetEmployeeOwnedRoadmapCountQueryHandler(_dbContext);

        // Act
        var result = await sut.Handle(new GetEmployeeOwnedRoadmapCountQuery(employeeId), TestContext.Current.CancellationToken);

        // Assert
        result.Should().Be(2);
    }
}
