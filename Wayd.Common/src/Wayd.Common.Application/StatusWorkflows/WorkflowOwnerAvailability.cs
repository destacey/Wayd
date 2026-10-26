using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Interfaces;
using Wayd.Common.Domain.StatusWorkflows;

namespace Wayd.Common.Application.StatusWorkflows;

/// <summary>
/// Which owner types workflow administration may show and change right now.
/// </summary>
/// <remarks>
/// An owner type whose module is released behind a feature flag (<see cref="WorkflowOwnerDescriptor.FeatureFlag"/>)
/// is absent while the flag is off: its workflows and assignments appear in no list and read as not found,
/// and none can be created, cloned or reassigned, so an unreleased module shows nowhere in Settings. Edits
/// to one workflow by id are not checked: they need an id that no read hands out while the flag is off.
/// Only administration asks this. Resolving a record's workflow does not, because the module's own
/// endpoints are gated by the same flag.
/// </remarks>
public interface IWorkflowOwnerAvailability : IScopedService
{
    /// <summary>
    /// The registered owner types whose feature flag is off. An owner type with no flag, or one no module
    /// registers, is never in it.
    /// </summary>
    Task<IReadOnlySet<string>> Unavailable(CancellationToken cancellationToken);

    /// <summary>Whether workflow administration may show and change workflows of the owner type.</summary>
    Task<bool> IsAvailable(string ownerType, CancellationToken cancellationToken);
}

/// <inheritdoc cref="IWorkflowOwnerAvailability"/>
public sealed class WorkflowOwnerAvailability(IFeatureFlags featureFlags) : IWorkflowOwnerAvailability
{
    private readonly IFeatureFlags _featureFlags = featureFlags;

    public async Task<IReadOnlySet<string>> Unavailable(CancellationToken cancellationToken)
    {
        var unavailable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var descriptor in WorkflowOwners.All)
        {
            if (descriptor.FeatureFlag is { } flag && !await _featureFlags.IsEnabled(flag, cancellationToken))
            {
                unavailable.Add(descriptor.Key);
            }
        }

        return unavailable;
    }

    public async Task<bool> IsAvailable(string ownerType, CancellationToken cancellationToken) =>
        !(await Unavailable(cancellationToken)).Contains(ownerType);
}
