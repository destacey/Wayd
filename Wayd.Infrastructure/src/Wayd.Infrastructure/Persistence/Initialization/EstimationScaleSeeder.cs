using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Events;
using Wayd.Planning.Domain.Models.PlanningPoker;
using NodaTime;

namespace Wayd.Infrastructure.Persistence.Initialization;

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

        foreach (var seed in seeds)
        {
            if (seed.IsSuccess)
            {
                dbContext.EstimationScales.Add(seed.Value);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static Result<EstimationScale> CreateScale(string name, string description, string[] estimates, Instant now)
    {
        var values = estimates.Select(v => new EstimationScaleValue(v, isEstimate: true))
            .Concat(_cards.Select(c => new EstimationScaleValue(c, isEstimate: false)));

        return EstimationScale.Create(name, description, values, EventActor.System, now);
    }
}
