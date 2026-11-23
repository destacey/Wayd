using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.Employees;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Models;
using Wayd.Common.Models;
using Wayd.Planning.Application.Roadmaps.Commands;
using Wayd.Planning.Domain.Interfaces.Roadmaps;
using Wayd.Planning.Domain.Models.Roadmaps;
using Wayd.Planning.IntegrationTests.Infrastructure;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// Moving a roadmap activity against a real SQL Server container.
/// </summary>
/// <remarks>
/// The refusal to move an activity under its own descendant walks the parent chain of the items EF loads, which
/// only a real provider does; an in-memory fake hands back the aggregate's own collections and so cannot show a
/// chain the handler failed to load.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class UpdateRoadmapActivityPlacementCommandHandlerTests(SqlServerDbContextFixture fixture)
{
    private static readonly LocalDateRange Year = new(new LocalDate(2027, 1, 4), new LocalDate(2027, 12, 17));

    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_ShouldRefuseAMoveUnderItsOwnGrandchild_AndSaveNothing()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var now = SqlServerDbContextFixture.FixedNow;

        await using var seed = _fixture.CreateContext();
        var owner = Employee.Create(new PersonName("Grace", null, "Hopper"), "E" + Guid.NewGuid().ToString("N")[..11], now,
            new EmailAddress($"grace.{Guid.NewGuid():N}@acme.example"), null, null, null, null, true, null, EventActor.System, now);
        seed.Employees.Add(owner);
        await seed.SaveChangesAsync(ct);

        var caller = new RoadmapActor(owner.Id, false);
        var roadmap = Roadmap.Create("Platform 2027", null, Year, Visibility.Public, [owner.Id], EventActor.System, now).Value;
        var platform = roadmap.CreateActivity(new Activity("Platform", null, Year), caller, EventActor.System, now).Value;
        var edge = roadmap.CreateActivity(new Activity("Edge", platform.Id, Year), caller, EventActor.System, now).Value;
        var cache = roadmap.CreateActivity(new Activity("Cache", edge.Id, Year), caller, EventActor.System, now).Value;
        seed.Roadmaps.Add(roadmap);
        await seed.SaveChangesAsync(ct);

        await using var context = _fixture.CreateContext();
        var principal = new Mock<ICurrentPrincipal>();
        principal.Setup(p => p.GetEmployeeId(It.IsAny<CancellationToken>())).ReturnsAsync(owner.Id);
        var sut = new UpdateRoadmapActivityPlacementCommandHandler(
            context, principal.Object, Mock.Of<ICurrentUser>(u => u.GetUserId() == "user-1"),
            Mock.Of<IDateTimeProvider>(p => p.Now == now), NullLogger<UpdateRoadmapActivityPlacementCommandHandler>.Instance);

        // Act
        var result = await sut.Handle(new UpdateRoadmapActivityPlacementCommand(roadmap.Id, cache.Id, platform.Id, 1), ct);

        // Assert
        result.IsFailure.Should().BeTrue();
        result.Error.Should().Be("Unable to move the Roadmap Item under itself or one of its own descendants.");

        await using var verify = _fixture.CreateContext();
        var parents = await verify.Set<BaseRoadmapItem>()
            .Where(i => i.RoadmapId == roadmap.Id)
            .ToDictionaryAsync(i => i.Id, i => i.ParentId, ct);
        parents[platform.Id].Should().BeNull();
        parents[edge.Id].Should().Be(platform.Id);
        parents[cache.Id].Should().Be(edge.Id);
    }

    private sealed record Activity(string Name, Guid? ParentId, LocalDateRange DateRange) : IUpsertRoadmapActivity
    {
        public string? Description => null;

        public string? Color => null;
    }
}
