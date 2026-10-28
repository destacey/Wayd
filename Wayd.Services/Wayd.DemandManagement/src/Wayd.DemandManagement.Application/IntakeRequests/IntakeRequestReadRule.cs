using System.Globalization;
using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.FeatureManagement;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Enums.DemandManagement;
using Wayd.Common.Domain.FeatureManagement;
using Wayd.Common.Domain.Notifications;
using Wayd.Common.Domain.StatusWorkflows.Enums;
using Wayd.DemandManagement.Domain;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeRequests;

/// <summary>
/// Who may read an intake request, by the rule its page applies: the summary tier of
/// <see cref="IntakeRequestReader"/>, behind the Demand Management feature flag.
/// </summary>
/// <remarks>
/// A request rerouted to a private desk the reader is not on, a participant removed from a private desk's
/// request, and a deleted request all drop out here, so their follows and notifications stop showing at once.
/// <see cref="Describe"/> returns only what the summary shows, never the substance, because a follower may
/// see only the summary. A request's history is read in tiers too, so <see cref="VisibleActivity"/> and
/// <see cref="ReadActivity"/> hold a follower to what the request's Activity section shows them.
/// </remarks>
public sealed class IntakeRequestReadRule(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    IFeatureFlags featureFlags) : IRecordReadRule
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly IFeatureFlags _featureFlags = featureFlags;

    /// <inheritdoc/>
    public string OwnerType => DemandManagementFollowOwners.IntakeRequest.Key;

    /// <inheritdoc/>
    public string ActivityAggregateType => nameof(IntakeRequest);

    /// <summary>Where each status category sits in a record's lifecycle.</summary>
    public static LifecycleCategory LifecycleOf(StatusCategory category) => category switch
    {
        StatusCategory.Proposed => LifecycleCategory.NotStarted,
        StatusCategory.Active => LifecycleCategory.Active,
        StatusCategory.Done => LifecycleCategory.Completed,
        _ => LifecycleCategory.Canceled,
    };

    /// <inheritdoc/>
    /// <remarks>
    /// Null while Demand Management is off, as its endpoints answer 404 then. A search matches the title
    /// anywhere, or the number exactly.
    /// </remarks>
    public async Task<IQueryable<RecordFacts>?> Readable(string? search, CancellationToken cancellationToken)
    {
        if (!await _featureFlags.IsEnabled(FeatureFlags.Names.DemandManagement, cancellationToken))
        {
            return null;
        }

        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        var requests = _dbContext.IntakeRequests.Where(reader.CanSeeSummary(_dbContext.IntakeDesks));

        if (search is not null)
        {
            requests = int.TryParse(search, NumberStyles.None, CultureInfo.InvariantCulture, out var key)
                ? requests.Where(r => r.Title.Contains(search) || r.Key == key)
                : requests.Where(r => r.Title.Contains(search));
        }

        return requests.Select(r => new RecordFacts
        {
            Id = r.Id,
            IsUnhealthy = false,
            IsClosed = r.StatusCategory == StatusCategory.Done || r.StatusCategory == StatusCategory.Removed,
        });
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The desk is the parent, as on the request's own page. Its assignee leads it, then the requester it is for.
    /// It ends at its need-by date.
    /// </remarks>
    public async Task<IReadOnlyDictionary<Guid, RecordSummary>> Describe(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        var employees = _dbContext.Employees;
        var requests = await _dbContext.IntakeRequests
            .Where(r => ids.Contains(r.Id))
            .Join(
                _dbContext.IntakeDesks,
                r => r.DeskId,
                d => d.Id,
                (r, d) => new
                {
                    r.Id,
                    r.Key,
                    r.Title,
                    r.StatusName,
                    r.StatusCategory,
                    NeedBy = r.NeedBy == null ? (LocalDate?)null : r.NeedBy.Date,
                    DeskId = d.Id,
                    DeskKey = d.Key,
                    DeskName = d.Name,
                    Assignee = employees
                        .Where(e => e.Id == r.AssigneeId)
                        .Select(e => new { e.Id, Name = e.Name.FirstName + " " + e.Name.LastName })
                        .FirstOrDefault(),
                    Requester = employees
                        .Where(e => e.Id == r.RequesterId)
                        .Select(e => new { e.Id, Name = e.Name.FirstName + " " + e.Name.LastName })
                        .FirstOrDefault(),
                })
            .ToListAsync(cancellationToken);

        return requests.ToDictionary(
            r => r.Id,
            r =>
            {
                List<RecordPerson> people = [];
                if (r.Assignee is not null)
                    people.Add(new RecordPerson("Assignee", r.Assignee.Name, r.Assignee.Id));
                if (r.Requester is not null)
                    people.Add(new RecordPerson("Requester", r.Requester.Name, r.Requester.Id));

                return new RecordSummary
                {
                    Key = r.Key.ToString(CultureInfo.InvariantCulture),
                    Name = r.Title,
                    ParentKey = r.DeskKey.ToString(CultureInfo.InvariantCulture),
                    ParentName = r.DeskName,
                    ParentId = r.DeskId,
                    StatusName = r.StatusName,
                    Lifecycle = LifecycleOf(r.StatusCategory),
                    People = people,
                    End = r.NeedBy,
                };
            });
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Each entry is kept when the reader holds its event's tier on the request it is about, by the rules the
    /// request's Activity section applies, so "last changed" never reveals when hidden fields changed.
    /// </remarks>
    public async Task<IQueryable<ActivityStamp>> VisibleActivity(IQueryable<ActivityStamp> activity, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        var desks = _dbContext.IntakeDesks;
        var visible = _dbContext.IntakeRequests.Where(reader.CanSeeSummary(desks));

        var summaryTypes = IntakeRequestEventTiers.EventTypesIn([IntakeRequestReadTier.Summary]).ToArray();
        var substanceTypes = IntakeRequestEventTiers.EventTypesIn([IntakeRequestReadTier.Substance]).ToArray();
        var internalTypes = IntakeRequestEventTiers.EventTypesIn([IntakeRequestReadTier.Internal]).ToArray();
        var substanceIds = visible.Where(reader.CanSeeSubstance(desks)).Select(r => r.Id);
        var internalIds = visible.Where(reader.CanSeeInternal()).Select(r => r.Id);

        return activity.Where(a => summaryTypes.Contains(a.EventType)
            || (substanceTypes.Contains(a.EventType) && substanceIds.Contains(a.RecordId))
            || (internalTypes.Contains(a.EventType) && internalIds.Contains(a.RecordId)));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// Each notification is kept when the reader holds, on the request it is about, the tier of the change it
    /// names, by the same rules as <see cref="VisibleActivity"/>: a follower who sees only the summary is not
    /// told that the description changed.
    /// </remarks>
    public async Task<IQueryable<Notification>> VisibleNotifications(IQueryable<Notification> notifications, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        var desks = _dbContext.IntakeDesks;
        var visible = _dbContext.IntakeRequests.Where(reader.CanSeeSummary(desks));

        var summaryEvents = IntakeRequestNotificationTiers.EventsIn(IntakeRequestReadTier.Summary);
        var substanceEvents = IntakeRequestNotificationTiers.EventsIn(IntakeRequestReadTier.Substance);
        var internalEvents = IntakeRequestNotificationTiers.EventsIn(IntakeRequestReadTier.Internal);
        var substanceIds = visible.Where(reader.CanSeeSubstance(desks)).Select(r => r.Id);
        var internalIds = visible.Where(reader.CanSeeInternal()).Select(r => r.Id);

        return notifications.Where(n => summaryEvents.Contains(n.Event)
            || (substanceEvents.Contains(n.Event) && substanceIds.Contains(n.OwnerId))
            || (internalEvents.Contains(n.Event) && internalIds.Contains(n.OwnerId)));
    }

    /// <inheritdoc/>
    /// <remarks>
    /// The requests whose substance the reader cannot see: who submitted a request and who takes part in it are
    /// substance, so the request's Activity section names nobody to them, and neither does a notification.
    /// </remarks>
    public async Task<IReadOnlySet<Guid>> ActorHiddenOn(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        var desks = _dbContext.IntakeDesks;
        var withSubstance = await _dbContext.IntakeRequests
            .Where(r => ids.Contains(r.Id))
            .Where(reader.CanSeeSubstance(desks))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        return ids.Except(withSubstance).ToHashSet();
    }

    /// <inheritdoc/>
    public async Task<PagedResponse<ActivityLogDto>> ReadActivity(
        Guid id,
        IActivityLogReader activityLogReader,
        PageRequest paging,
        CancellationToken cancellationToken)
    {
        var reader = await _currentPrincipal.ResolveIntakeRequestReader(_dbContext, cancellationToken);
        var visible = _dbContext.IntakeRequests
            .AsNoTracking()
            .Where(r => r.Id == id)
            .Where(reader.CanSeeSummary(_dbContext.IntakeDesks));

        return await new IntakeRequestActivity(_dbContext, activityLogReader).Read(visible, reader, paging, cancellationToken)
            ?? new PagedResponse<ActivityLogDto>([], 0, paging.Page, paging.PageSize);
    }
}
