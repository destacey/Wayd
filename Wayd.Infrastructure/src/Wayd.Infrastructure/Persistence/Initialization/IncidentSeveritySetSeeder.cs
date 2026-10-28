using CSharpFunctionalExtensions;
using Microsoft.EntityFrameworkCore;
using Wayd.ProductManagement.Domain.Models;

namespace Wayd.Infrastructure.Persistence.Initialization;

public class IncidentSeveritySetSeeder : ICustomSeeder
{
    public async Task Initialize(
        WaydDbContext dbContext,
        IDateTimeProvider dateTimeProvider,
        CancellationToken cancellationToken)
    {
        if (await dbContext.IncidentSeveritySets.AnyAsync(cancellationToken))
        {
            return;
        }

        var sevScale = IncidentSeveritySet.CreateSystem(
            "SEV",
            "Four-level SEV scale with SEV-1 as the highest severity.",
            ["SEV-1", "SEV-2", "SEV-3", "SEV-4"]);

        var criticalScale = IncidentSeveritySet.CreateSystem(
            "Critical / High / Medium / Low",
            "Four-level scale from Critical down to Low.",
            ["Critical", "High", "Medium", "Low"]);

        if (sevScale.IsFailure || criticalScale.IsFailure)
        {
            throw new InvalidOperationException(
                $"Failed to seed incident severity sets: {sevScale.Error} {criticalScale.Error}");
        }

        dbContext.IncidentSeveritySets.Add(sevScale.Value);
        dbContext.IncidentSeveritySets.Add(criticalScale.Value);
        await dbContext.SaveChangesAsync(cancellationToken);

        dbContext.IncidentSeveritySetAssignments.Add(
            IncidentSeveritySetAssignment.Create(sevScale.Value.Id));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
