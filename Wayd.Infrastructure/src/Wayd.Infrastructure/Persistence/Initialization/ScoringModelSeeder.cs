using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Scoring;

namespace Wayd.Infrastructure.Persistence.Initialization;

public class ScoringModelSeeder : ICustomSeeder
{
    public async Task Initialize(WaydDbContext dbContext, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        // Initial seed only: provide a starter WSJF model when no scoring models exist yet.
        // Once any model is present (seeded or admin-created) this seeder stays out of the way.
        if (await dbContext.ScoringModels.AnyAsync(cancellationToken))
            return;

        dbContext.ScoringModels.Add(CreateWsjfModel());

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <summary>
    /// Weighted Shortest Job First. Business Value, Time Criticality and Risk Reduction /
    /// Opportunity Enablement are rated on a shared relative scale and summed into the Cost of Delay;
    /// Job Size is rated on the same scale, and WSJF = Cost of Delay / Job Size is the primary score.
    /// Seeded in the Proposed state so an admin can review and activate it.
    /// </summary>
    private static ScoringModel CreateWsjfModel()
    {
        // Modified-Fibonacci relative scale, the conventional choice for WSJF inputs.
        (string Label, decimal Value)[] relativeScale =
        [
            ("1", 1m),
            ("2", 2m),
            ("3", 3m),
            ("5", 5m),
            ("8", 8m),
            ("13", 13m),
            ("20", 20m),
        ];

        return ScoringModel.Create(
            "WSJF",
            "Weighted Shortest Job First. Prioritizes by Cost of Delay relative to Job Size; "
                + "the highest WSJF score is the most economically valuable to deliver next.",
            scales:
            [
                ("Relative (Fibonacci)", relativeScale),
            ],
            criteria:
            [
                ("Business Value", "BV", "Value to the business or customer of delivering this item.", null, "Relative (Fibonacci)"),
                ("Time Criticality", "TC", "How the value decays over time, or any fixed deadline.", null, "Relative (Fibonacci)"),
                ("Risk Reduction / Opportunity Enablement", "RR", "Risk this reduces or future opportunity it unlocks.", null, "Relative (Fibonacci)"),
                ("Job Size", "JS", "Relative effort or duration to deliver this item.", null, "Relative (Fibonacci)"),
            ],
            outputs:
            [
                ("Cost of Delay", "CoD", "BV + TC + RR", false),
                ("WSJF", "WSJF", "CoD / JS", true),
            ]);
    }
}
