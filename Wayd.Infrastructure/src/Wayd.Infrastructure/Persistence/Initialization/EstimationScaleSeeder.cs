using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Events;
using Wayd.Common.Domain.Events.Planning.EstimationScales;
using Wayd.Common.Domain.Models.Planning.EstimationScales;
using Wayd.DemandManagement.Domain.Models.Estimation;
using Wayd.Infrastructure.Persistence.Activities;
using Wayd.Planning.Domain.Models.PlanningPoker;
using NodaTime;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// Seeds the estimation scales an install starts with, recording each one's creation in the activity log and
/// giving Demand Management its copy of each.
/// </summary>
/// <remarks>
/// Initial seed only: once any scale exists this stays out of the way. The creation events are taken off the
/// scales so SaveChanges cannot publish them, because Wolverine cannot route while seeders run, before the
/// host starts; the copies those events would have made are written here instead.
/// </remarks>
public class EstimationScaleSeeder : ICustomSeeder
{
    /// <summary>
    /// The cards every seeded scale ends with: votable, never a size.
    /// </summary>
    private static readonly string[] _cards = ["?", "∞", "☕"];

    public async Task Initialize(WaydDbContext dbContext, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        if (await dbContext.EstimationScales.AnyAsync(cancellationToken))
            return;

        var now = dateTimeProvider.Now;
        var seeds = new[]
        {
            CreateScale("Fibonacci", "Fibonacci sequence values commonly used in agile estimation.",
                ["0", "1", "2", "3", "5", "8", "13", "21"], now),
            CreateScale("Modified Fibonacci", "Modified Fibonacci sequence.",
                ["0", "1", "2", "3", "5", "8", "13", "20", "40", "100"], now),
            CreateScale("T-Shirt Sizes", "Simple relative sizing using t-shirt sizes.",
                ["XXS", "XS", "S", "M", "L", "XL", "XXL"], now),
            CreateScale("Simple", "Simple 1-5 scale for quick estimation.",
                ["1", "2", "3", "4", "5"], now),
        };

        List<EstimationScale> scales = [.. seeds.Where(s => s.IsSuccess).Select(s => s.Value)];

        // The creation events wait for the key, so they are post-persistence actions.
        Action[] raiseCreationEvents = [.. scales.SelectMany(s => s.PostPersistenceActions)];
        foreach (var scale in scales)
        {
            scale.ClearDomainEvents();
            scale.ClearPostPersistenceActions();
            dbContext.EstimationScales.Add(scale);
        }

        // One transaction: a scale committed without its activity or its copy would stay that way, because any
        // scale existing stops every later run from seeding.
        var strategy = dbContext.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var unitOfWork = await dbContext.BeginUnitOfWork(cancellationToken);

            await dbContext.SaveChangesAsync(cancellationToken);

            foreach (var raise in raiseCreationEvents)
            {
                raise();
            }

            var ordinal = 0;
            foreach (var scale in scales)
            {
                foreach (var domainEvent in scale.DomainEvents)
                {
                    dbContext.ActivityLogs.Add(
                        ActivityLogEntryFactory.CreateActivityLogEntry(domainEvent, scale, ordinal++, correlationId: null));
                }

                scale.ClearDomainEvents();
                dbContext.DemandEstimationScales.Add(new DemandEstimationScale(Snapshot(scale), now));
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            await unitOfWork.CommitAsync(cancellationToken);
        });
    }

    private static EstimationScaleSnapshot Snapshot(EstimationScale scale) => new(
        scale.Id,
        scale.Key,
        scale.Name,
        scale.IsActive,
        [.. scale.Versions
            .OrderBy(v => v.Number)
            .Select(v => new EstimationScaleVersionValues(
                v.Id,
                v.Number,
                [.. v.Values.Select(x => new EstimationScaleValueValues(x.Value, x.IsEstimate))]))]);

    private static Result<EstimationScale> CreateScale(string name, string description, string[] estimates, Instant now)
    {
        var values = estimates.Select(v => new EstimationScaleValue(v, isEstimate: true))
            .Concat(_cards.Select(c => new EstimationScaleValue(c, isEstimate: false)));

        return EstimationScale.Create(name, description, values, EventActor.System, now);
    }
}
