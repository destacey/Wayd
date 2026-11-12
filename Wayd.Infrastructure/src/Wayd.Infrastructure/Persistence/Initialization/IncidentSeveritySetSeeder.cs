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
            [
                ("SEV-1", IncidentSeverityLevel.DefaultColorForSortOrder(0)),
                ("SEV-2", IncidentSeverityLevel.DefaultColorForSortOrder(1)),
                ("SEV-3", IncidentSeverityLevel.DefaultColorForSortOrder(2)),
                ("SEV-4", IncidentSeverityLevel.DefaultColorForSortOrder(3)),
            ]);

        var criticalScale = IncidentSeveritySet.CreateSystem(
            "Critical / High / Medium / Low",
            "Four-level scale from Critical down to Low.",
            [
                ("Critical", IncidentSeverityLevel.DefaultColorForSortOrder(0)),
                ("High", IncidentSeverityLevel.DefaultColorForSortOrder(1)),
                ("Medium", IncidentSeverityLevel.DefaultColorForSortOrder(2)),
                ("Low", IncidentSeverityLevel.DefaultColorForSortOrder(3)),
            ]);

        if (sevScale.IsFailure || criticalScale.IsFailure)
        {
            throw new InvalidOperationException(
                $"Failed to seed incident severity sets: {sevScale.Error} {criticalScale.Error}");
        }

        dbContext.IncidentSeveritySets.Add(sevScale.Value);
        dbContext.IncidentSeveritySets.Add(criticalScale.Value);
        dbContext.IncidentSeveritySetAssignments.Add(
            IncidentSeveritySetAssignment.Create(sevScale.Value.Id));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
