using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Links;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Incidents;

/// <summary>
/// Who may read and change an incident's links, and what a link change means for the incident.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Reading</strong> needs what reading the incident needs: the Incidents View permission, with incident
/// management turned on.
/// </para>
/// <para>
/// <strong>Changing</strong> is editing the incident: the Incidents Update permission and a linked employee, whom the
/// timeline names.
/// </para>
/// <para>
/// <strong>Every change is an entry on the incident's timeline</strong>, written in the same save as the link.
/// </para>
/// </remarks>
public sealed class IncidentLinkOwnerType(
    IProductManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    IFeatureFlags featureFlags) : ILinkOwnerType
{
    /// <summary>The longest label a timeline link entry holds.</summary>
    private const int LabelMaxLength = 256;

    private static readonly string ViewPermission =
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Incidents);

    private static readonly string UpdatePermission =
        ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.Incidents);

    private readonly IProductManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IFeatureFlags _featureFlags = featureFlags;

    /// <inheritdoc/>
    public string OwnerType => ProductManagementLinkOwners.Incident.Key;

    /// <inheritdoc/>
    public async Task<LinkOwnerAccess?> Resolve(Guid ownerId, CancellationToken cancellationToken)
    {
        if (!await _featureFlags.IsEnabled(FeatureFlags.Names.ProductManagement, cancellationToken)
            || !await _featureFlags.IsEnabled(FeatureFlags.Names.IncidentManagement, cancellationToken))
            return null;

        if (!await _currentPrincipal.HoldsPermission(ViewPermission, cancellationToken))
            return null;

        if (!await _dbContext.Incidents.AnyAsync(i => i.Id == ownerId, cancellationToken))
            return null;

        var canManage = await _currentPrincipal.GetEmployeeId(cancellationToken) is not null
            && await _currentPrincipal.HasPermission(UpdatePermission, cancellationToken);

        return new LinkOwnerAccess(canManage);
    }

    /// <inheritdoc/>
    /// <remarks>
    /// A change with no employee behind it — an import — writes no entry: the timeline names who made each change.
    /// </remarks>
    public async Task OnChanged(LinkChange change, CancellationToken cancellationToken)
    {
        if (change.Actor.EmployeeId is null)
            return;

        var incident = await _dbContext.Incidents.FirstOrDefaultAsync(i => i.Id == change.OwnerId, cancellationToken);
        if (incident is null)
            return;

        var (action, label, from, to) = change.Kind switch
        {
            LinkChangeKind.Added => (IncidentTimelineLinkAction.Added, change.Details.Name, (string?)null, (string?)null),
            LinkChangeKind.Removed => (IncidentTimelineLinkAction.Removed, change.Details.Name, null, null),
            _ => (IncidentTimelineLinkAction.Updated, $"{change.Previous!.Name} → {change.Details.Name}", change.Previous.Name, change.Details.Name),
        };

        // Two names at their longest, joined, are longer than a label may be.
        if (label.Length > LabelMaxLength)
            label = label[..LabelMaxLength];

        var recorded = incident.RecordLinkTimelineEntry(
            IncidentTimelineLinkKind.Generic,
            action,
            label,
            from,
            to,
            change.Actor,
            change.Timestamp,
            change.Timestamp,
            change.LinkId);

        if (recorded.IsFailure)
            throw new InvalidOperationException($"The incident's timeline refused a link change: {recorded.Error}");
    }
}
