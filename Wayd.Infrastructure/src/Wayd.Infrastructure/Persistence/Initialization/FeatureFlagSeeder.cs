using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.FeatureManagement;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// Makes the stored feature flags match <see cref="FeatureFlags"/>: creates missing flags, reapplies
/// each definition's name, description and module to its flag, and archives flags the code no longer
/// defines. Whether a flag is enabled is never changed, except that an archived flag is turned off.
/// </summary>
public class FeatureFlagSeeder : ICustomSeeder
{
    /// <inheritdoc />
    public async Task Initialize(WaydDbContext dbContext, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        var definitions = FeatureFlags.All
            .ToDictionary(d => d.Name.Trim(), StringComparer.OrdinalIgnoreCase);

        var existing = await dbContext.FeatureFlags.ToListAsync(cancellationToken);
        var changed = false;

        foreach (var flag in existing)
        {
            if (definitions.Remove(flag.Name, out var definition))
                changed |= flag.SyncWith(definition);
            else if (!flag.IsArchived)
                changed |= flag.Archive().IsSuccess;
        }

        foreach (var definition in definitions.Values)
        {
            var result = FeatureFlag.Create(definition);
            if (result.IsFailure)
                throw new InvalidOperationException($"Feature flag definition '{definition.Name}' is invalid: {result.Error}");

            dbContext.FeatureFlags.Add(result.Value);
            changed = true;
        }

        if (changed)
            await dbContext.SaveChangesAsync(cancellationToken);
    }
}
