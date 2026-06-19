using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.FeatureManagement;

namespace Wayd.Infrastructure.Persistence.Initialization;

public class FeatureFlagSeeder : ICustomSeeder
{
    public async Task Initialize(WaydDbContext dbContext, IDateTimeProvider dateTimeProvider, CancellationToken cancellationToken)
    {
        var definitions = GetAllDefinitions();
        if (definitions.Length == 0)
            return;

        var existingNames = await dbContext.FeatureFlags
            .Select(f => f.Name)
            .ToListAsync(cancellationToken);

        var existingNamesSet = new HashSet<string>(existingNames, StringComparer.OrdinalIgnoreCase);
        var hasNew = false;

        foreach (var definition in definitions)
        {
            if (existingNamesSet.Contains(definition.Name))
                continue;

            var result = FeatureFlag.Create(definition.Name, definition.DisplayName, definition.Description, false, isSystem: true);
            if (result.IsSuccess)
            {
                dbContext.FeatureFlags.Add(result.Value);
                hasNew = true;
            }
        }

        if (hasNew)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private static FeatureFlagDefinition[] GetAllDefinitions()
    {
        return typeof(FeatureFlags)
            .GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(f => f.FieldType == typeof(FeatureFlagDefinition))
            .Select(f => (FeatureFlagDefinition)f.GetValue(null)!)
            .ToArray();
    }
}
