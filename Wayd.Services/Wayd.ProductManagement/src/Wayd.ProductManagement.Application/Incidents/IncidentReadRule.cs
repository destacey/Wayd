using System.Globalization;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Authorization;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.ProductManagement;
using Wayd.Common.Domain.Notifications;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Incidents;

/// <summary>
/// Who may read an incident, by the rule the incident endpoints apply: the View Incidents permission.
/// </summary>
public sealed class IncidentReadRule(
    IProductManagementDbContext productManagementDbContext,
    ICurrentPrincipal currentPrincipal) : IRecordReadRule
{
    /// <summary>The permission the incident read endpoints require.</summary>
    public static readonly string ViewPermission =
        ApplicationPermission.NameFor(ApplicationAction.View, ApplicationResource.Incidents);

    private static readonly ProductStatusAlias[] ClosedAliases =
        [ProductStatusAlias.Closed, ProductStatusAlias.Canceled];

    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;

    /// <inheritdoc/>
    public string OwnerType => ProductManagementFollowOwners.Incident.Key;

    /// <inheritdoc/>
    public string ActivityAggregateType => "Incident";

    /// <inheritdoc/>
    public static LifecycleCategory LifecycleOf(ProductStatusAlias alias, StatusCategory category) =>
        ClosedAliases.Contains(alias) || category is StatusCategory.Done or StatusCategory.Removed
            ? category == StatusCategory.Removed
                ? LifecycleCategory.Canceled
                : LifecycleCategory.Completed
            : LifecycleCategory.Active;

    /// <inheritdoc/>
    public async Task<IQueryable<RecordFacts>?> Readable(string? search, CancellationToken cancellationToken)
    {
        if (!await _currentPrincipal.HasPermission(ViewPermission, cancellationToken))
        {
            return null;
        }

        var incidents = _productManagementDbContext.Incidents.AsQueryable();

        if (search is not null)
        {
            incidents = int.TryParse(search, NumberStyles.None, CultureInfo.InvariantCulture, out var key)
                ? incidents.Where(i => i.Title.Contains(search) || i.Key == key)
                : incidents.Where(i => i.Title.Contains(search));
        }

        return incidents.Select(i => new RecordFacts
        {
            Id = i.Id,
            IsUnhealthy = false,
            IsClosed = ClosedAliases.Contains((ProductStatusAlias)i.StatusAliasValue)
                || i.StatusCategory == StatusCategory.Removed,
        });
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyDictionary<Guid, RecordSummary>> Describe(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        var incidents = await (
            from i in _productManagementDbContext.Incidents
            join e in _productManagementDbContext.Employees on i.CommanderEmployeeId equals e.Id
            where ids.Contains(i.Id)
            select new
            {
                i.Id,
                i.Key,
                i.Title,
                i.StatusName,
                i.StatusCategory,
                i.StatusAliasValue,
                i.NoticedAt,
                i.RecoveredAt,
                CommanderName = e.Name.FullName,
            }).ToListAsync(cancellationToken);

        return incidents.ToDictionary(
            i => i.Id,
            i =>
            {
                var alias = (ProductStatusAlias)i.StatusAliasValue;
                return new RecordSummary
                {
                    Key = i.Key.ToString(CultureInfo.InvariantCulture),
                    Name = i.Title,
                    StatusName = i.StatusName,
                    Lifecycle = LifecycleOf(alias, i.StatusCategory),
                    People =
                    [
                        new RecordPerson(
                            "Commander",
                            i.CommanderName,
                            null),
                    ],
                };
            });
    }

    /// <inheritdoc/>
    public Task<IQueryable<ActivityStamp>> VisibleActivity(IQueryable<ActivityStamp> activity, CancellationToken cancellationToken) =>
        Task.FromResult(activity);

    /// <inheritdoc/>
    public Task<IQueryable<Notification>> VisibleNotifications(IQueryable<Notification> notifications, CancellationToken cancellationToken) =>
        Task.FromResult(notifications);

    /// <inheritdoc/>
    public Task<IReadOnlySet<Guid>> ActorHiddenOn(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());

    /// <inheritdoc/>
    public Task<PagedResponse<ActivityLogDto>> ReadActivity(
        Guid id,
        IActivityLogReader activityLogReader,
        PageRequest paging,
        CancellationToken cancellationToken) =>
        activityLogReader.Read(id, ActivityAggregateType, paging, cancellationToken);
}
