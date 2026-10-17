using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models;
using Wayd.Common.Models;
using Wayd.Planning.Application.Roadmaps.Dtos;
using Wayd.Planning.Application.Roadmaps.Queries;
using Wayd.Planning.Domain.Interfaces.Roadmaps;
using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Planning.IntegrationTests.Infrastructure;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// A roadmap's activities against a real SQL Server container.
/// </summary>
/// <remarks>
/// The tree is assembled from the parent and child navigations EF fixes up as it loads the rows, which only a
/// real provider does; an in-memory fake hands back the aggregate's own collections and so cannot fail.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetRoadmapActivitiesQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDateRange Year = new(new LocalDate(2027, 1, 4), new LocalDate(2027, 12, 17));

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_NestsEachChildActivityUnderItsParent()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var now = SqlServerDbContextFixture.FixedNow;

        await using var seed = _fixture.CreateContext();
        var manager = Employee.Create(new PersonName("Grace", null, "Hopper"), "E" + Guid.NewGuid().ToString("N")[..11], now,
            new EmailAddress($"grace.{Guid.NewGuid():N}@acme.example"), null, null, null, null, true, null, EventActor.System, now);
        seed.Employees.Add(manager);
        await seed.SaveChangesAsync(ct);

        var roadmap = Roadmap.Create("Platform 2027", null, Year, Visibility.Public, [manager.Id], EventActor.System, now).Value;
        var platform = roadmap.CreateActivity(new Activity("Platform", null, Year), manager.Id, EventActor.System, now).Value;
        var edgeCache = roadmap.CreateActivity(new Activity("Edge cache rollout", platform.Id, Year), manager.Id, EventActor.System, now).Value;
        seed.Roadmaps.Add(roadmap);
        await seed.SaveChangesAsync(ct);

        await using var context = _fixture.CreateContext();
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(manager.Id);
        var sut = new GetRoadmapActivitiesQueryHandler(context, principal.Object);

        // Act
        var activities = await sut.Handle(new GetRoadmapActivitiesQuery(new IdOrKey(roadmap.Key)), ct);

        // Assert
        var root = activities.Should().ContainSingle().Subject;
        root.Id.Should().Be(platform.Id);
        root.Start.Should().Be(Year.Start);
        root.End.Should().Be(Year.End);
        root.Children.Should().ContainSingle().Which.Id.Should().Be(edgeCache.Id);
    }

    private sealed record Activity(string Name, Guid? ParentId, LocalDateRange DateRange) : IUpsertRoadmapActivity
    {
        public string? Description => null;

        public string? Color => null;
    }
}
