using Wayd.Common.Application.Discussions;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Discussions;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Incidents;

/// <summary>
/// Who may read, post, and moderate in an incident's conversation.
/// </summary>
/// <remarks>
/// Reading follows the Incidents View permission. Anyone with a linked employee who is not read-only may post;
/// moderating requires Incidents Update. There are no internal notes.
/// </remarks>
public sealed class IncidentDiscussionChannelType(
    IProductManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    IFeatureFlags featureFlags) : IDiscussionChannelType
{
    private static readonly string ViewPermission =
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Incidents);

    private static readonly string UpdatePermission =
        ApplicationPermission.NameFor(ApplicationAction.Update, ApplicationResource.Incidents);

    private readonly IProductManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IFeatureFlags _featureFlags = featureFlags;

    /// <inheritdoc/>
    public string ChannelType => ProductManagementDiscussionChannels.Incident.Key;

    /// <inheritdoc/>
    public async Task<DiscussionAccess?> Resolve(Guid ownerId, CancellationToken cancellationToken)
    {
        if (!await _featureFlags.IsEnabled(FeatureFlags.Names.ProductManagement, cancellationToken)
            || !await _featureFlags.IsEnabled(FeatureFlags.Names.IncidentManagement, cancellationToken))
            return null;

        if (!await _currentPrincipal.HoldsPermission(ViewPermission, cancellationToken))
            return null;

        if (!await _dbContext.Incidents.AnyAsync(i => i.Id == ownerId, cancellationToken))
            return null;

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var mayWrite = employeeId is not null && !_currentPrincipal.IsReadOnly;
        var canModerate = mayWrite
            && await _currentPrincipal.HasPermission(UpdatePermission, cancellationToken);

        return new DiscussionAccess(
            CanReadInternal: false,
            CanPost: mayWrite,
            CanPostInternal: false,
            CanModerate: canModerate);
    }

    /// <inheritdoc/>
    public async Task<IReadOnlySet<Guid>> MentionableAmong(
        Guid ownerId,
        IReadOnlyCollection<Guid> employeeIds,
        DiscussionMessageKind kind,
        CancellationToken cancellationToken)
    {
        if (kind != DiscussionMessageKind.Public || employeeIds.Count == 0)
            return new HashSet<Guid>();

        var active = await _dbContext.Employees
            .Where(e => employeeIds.Contains(e.Id) && e.IsActive)
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        return active.ToHashSet();
    }

    /// <inheritdoc/>
    public Task OnPosted(DiscussionChannel channel, DiscussionMessage message, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public Task OnEdited(
        DiscussionChannel channel,
        DiscussionMessage message,
        IReadOnlyCollection<Guid> addedMentionIds,
        CancellationToken cancellationToken) =>
        Task.CompletedTask;

    /// <inheritdoc/>
    public Task<DiscussionSettingsAccess?> ResolveSettings(Guid? scopeId, CancellationToken cancellationToken) =>
        Task.FromResult<DiscussionSettingsAccess?>(null);
}
