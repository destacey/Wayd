using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Infrastructure.Persistence.Initialization;

/// <summary>
/// Rebuilds the alias-name lookup from every registered <see cref="WorkflowOwnerDescriptor"/>.
/// </summary>
/// <remarks>
/// Runs on every startup rather than once, because the descriptors are code: a module renaming an alias
/// or adding one must be reflected without a migration, which is the whole reason the mapping is data.
/// Rows for owner types no longer registered are left alone — a module temporarily out of the build
/// should not take the names of its historical rows with it.
/// </remarks>
public class WorkflowAliasNameSeeder : ICustomSeeder
{
    public async Task Initialize(WaydDbContext dbContext, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        var descriptors = WorkflowOwners.All;
        if (descriptors.Count == 0)
        {
            return;
        }

        var ownerTypes = descriptors.Select(d => d.Key).ToArray();

        var existing = await dbContext.WorkflowAliasNames
            .Where(a => ownerTypes.Contains(a.OwnerType))
            .ToDictionaryAsync(a => (a.OwnerType, a.Alias), cancellationToken);

        var changed = false;

        foreach (var descriptor in descriptors)
        {
            foreach (var (alias, name) in descriptor.Aliases)
            {
                if (existing.TryGetValue((descriptor.Key, alias), out var row))
                {
                    if (!string.Equals(row.Name, name, StringComparison.Ordinal))
                    {
                        row.Rename(name);
                        changed = true;
                    }

                    continue;
                }

                dbContext.WorkflowAliasNames.Add(new WorkflowAliasName(descriptor.Key, alias, name));
                changed = true;
            }

            // An alias a module has withdrawn: the value may still be held by existing records, so the
            // row stays rather than leaving those rows unnameable.
        }

        if (changed)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
