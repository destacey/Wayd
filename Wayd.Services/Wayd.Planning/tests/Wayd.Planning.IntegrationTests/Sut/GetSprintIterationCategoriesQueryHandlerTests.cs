using Wayd.Common.Application.Requests.Planning.Queries;
using Wayd.Common.Domain.Enums.Planning;
using Wayd.Common.Domain.Events;
using Wayd.Common.Models;
using Wayd.Infrastructure.Persistence.Context;
using Wayd.Planning.Application.PlanningSprints.Queries;
using Wayd.Planning.Domain.Models;
using Wayd.Planning.IntegrationTests.Infrastructure;

namespace Wayd.Planning.IntegrationTests.Sut;

/// <summary>
/// Proves that a sprint's mapped iteration category is read through the mapping, and that a mapping left behind
/// by a soft-deleted planning interval is not.
/// </summary>
/// <remarks>
/// The deleted interval's mapping rows remain; only the real query filter leaves the interval's navigation null,
/// which the fake context cannot show.
/// </remarks>
[Collection(SqlServerTestCollection.Name)]
public sealed class GetSprintIterationCategoriesQueryHandlerTests(SqlServerDbContextFixture fixture)
{
    private readonly SqlServerDbContextFixture _fixture = fixture;

    [Fact]
    public async Task Handle_ReturnsEachMappedSprintsCategory_AndSkipsUnmappedAndDeletedIntervals()
    {
        // Arrange
        var ct = TestContext.Current.CancellationToken;
        var now = SqlServerDbContextFixture.FixedNow;

        Guid ipSprintId, developmentSprintId, unmappedSprintId, deletedIntervalSprintId;
        await using (var seed = _fixture.CreateContext())
        {
            var team = await PlanningSeed.Team(seed, ct);
            var ipSprint = await PlanningSeed.Sprint(seed, team.Id, ct);
            var developmentSprint = await PlanningSeed.Sprint(seed, team.Id, ct);
            var unmappedSprint = await PlanningSeed.Sprint(seed, team.Id, ct);
            var deletedIntervalSprint = await PlanningSeed.Sprint(seed, team.Id, ct);

            // The last iteration an interval generates is its IP iteration.
            var interval = await NewInterval(seed, team.Id, ct);
            interval.MapSprintToIteration(interval.Iterations.Last().Id, ipSprint, EventActor.System, now).IsSuccess.Should().BeTrue();
            interval.MapSprintToIteration(interval.Iterations.First().Id, developmentSprint, EventActor.System, now).IsSuccess.Should().BeTrue();

            var deletedInterval = await NewInterval(seed, team.Id, ct);
            deletedInterval.MapSprintToIteration(deletedInterval.Iterations.Last().Id, deletedIntervalSprint, EventActor.System, now).IsSuccess.Should().BeTrue();
            await seed.SaveChangesAsync(ct);

            deletedInterval.IsDeleted = true;
            await seed.SaveChangesAsync(ct);

            ipSprintId = ipSprint.Id;
            developmentSprintId = developmentSprint.Id;
            unmappedSprintId = unmappedSprint.Id;
            deletedIntervalSprintId = deletedIntervalSprint.Id;
        }

        // Act
        IReadOnlyDictionary<Guid, IterationCategory> result;
        await using (var context = _fixture.CreateContext())
        {
            var handler = new GetSprintIterationCategoriesQueryHandler(context);
            result = await handler.Handle(new GetSprintIterationCategoriesQuery([ipSprintId, developmentSprintId, unmappedSprintId, deletedIntervalSprintId]), ct);
        }

        // Assert
        result.Should().BeEquivalentTo(new Dictionary<Guid, IterationCategory>
        {
            [ipSprintId] = IterationCategory.InnovationAndPlanning,
            [developmentSprintId] = IterationCategory.Development,
        });
    }

    private static async Task<PlanningInterval> NewInterval(WaydDbContext context, Guid teamId, CancellationToken ct)
    {
        var now = SqlServerDbContextFixture.FixedNow;
        var interval = PlanningInterval.Create($"Atlas PI {Guid.NewGuid():N}"[..20], null,
            new LocalDateRange(new LocalDate(2026, 1, 5), new LocalDate(2026, 3, 1)), 4, "Iteration ", EventActor.System, now).Value;
        interval.ManageTeams([teamId], EventActor.System, now);
        context.PlanningIntervals.Add(interval);
        await context.SaveChangesAsync(ct);

        return interval;
    }
}
