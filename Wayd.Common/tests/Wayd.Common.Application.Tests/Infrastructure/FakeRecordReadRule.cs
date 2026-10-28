using Wayd.Common.Application.Activities;
using Wayd.Common.Application.Activities.Dtos;
using Wayd.Common.Application.Models;
using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Enums;
using Wayd.Common.Domain.Notifications;
using Wayd.Tests.Shared.Infrastructure;

namespace Wayd.Common.Application.Tests.Infrastructure;

/// <summary>
/// A followable owner type whose records and read rule a test sets directly.
/// </summary>
public sealed class FakeRecordReadRule(string ownerType) : IRecordReadRule
{
    private readonly List<FakeRecord> _records = [];
    private readonly HashSet<Guid> _hidden = [];
    private readonly HashSet<Guid> _goneBeforeDescribe = [];

    public string OwnerType { get; } = ownerType;

    /// <summary>The aggregate type a test logs this type's activity under.</summary>
    public const string ActivityType = "Test";

    public string ActivityAggregateType => ActivityType;

    /// <summary>Whether the caller holds this type's view permission. When false, nothing is readable.</summary>
    public bool CanView { get; set; } = true;

    /// <summary>How many times <see cref="Describe"/> has been asked, and with which ids last.</summary>
    public int DescribeCallCount { get; private set; }

    public IReadOnlyCollection<Guid> LastDescribedIds { get; private set; } = [];

    /// <summary>The search the last call to <see cref="Readable"/> was given.</summary>
    public string? LastSearch { get; private set; }

    public FakeRecord Add(
        string key,
        string name,
        string? parentKey = null,
        string? parentName = null,
        HealthStatus? health = null,
        LifecycleCategory lifecycle = LifecycleCategory.Active,
        Guid? id = null)
    {
        var record = new FakeRecord(id ?? Guid.NewGuid(), key, name, parentKey, parentName, health, lifecycle);
        _records.Add(record);
        return record;
    }

    /// <summary>
    /// Event types the caller may not see in any record's history, as a rule that reads history in tiers would
    /// withhold them.
    /// </summary>
    public HashSet<string> HiddenEventTypes { get; } = [];

    /// <summary>
    /// Changes the caller may not be told about on any record, as a rule that reads history in tiers would
    /// withhold them.
    /// </summary>
    public HashSet<NotificationEvent> HiddenNotificationEvents { get; } = [];

    /// <summary>Records on which the caller may not be told who made a change.</summary>
    public HashSet<Guid> ActorHiddenRecords { get; } = [];

    /// <summary>Makes one record unreadable to the caller while it still exists, as a row-level rule would.</summary>
    public void Hide(Guid id) => _hidden.Add(id);

    /// <summary>Deletes a record, leaving any follows of it behind.</summary>
    public void Delete(Guid id) => _records.RemoveAll(r => r.Id == id);

    /// <summary>
    /// Leaves a record readable but missing when it is described, as when it is deleted between a page's query
    /// and the read that names its records.
    /// </summary>
    public void DeleteBeforeDescribe(Guid id) => _goneBeforeDescribe.Add(id);

    public Task<IQueryable<RecordFacts>?> Readable(string? search, CancellationToken cancellationToken)
    {
        LastSearch = search;

        IQueryable<RecordFacts>? readable = CanView
            ? _records.AsDbSet()
                .Where(r => !_hidden.Contains(r.Id))
                .Where(r => search == null || r.Name.Contains(search, StringComparison.OrdinalIgnoreCase) || r.Key == search)
                .Select(r => new RecordFacts
                {
                    Id = r.Id,
                    IsUnhealthy = r.Health == HealthStatus.Unhealthy,
                    IsClosed = r.Lifecycle == LifecycleCategory.Completed || r.Lifecycle == LifecycleCategory.Canceled,
                })
            : null;

        return Task.FromResult(readable);
    }

    public Task<IReadOnlyDictionary<Guid, RecordSummary>> Describe(
        IReadOnlyCollection<Guid> ids,
        CancellationToken cancellationToken)
    {
        DescribeCallCount++;
        LastDescribedIds = ids;

        IReadOnlyDictionary<Guid, RecordSummary> summaries = _records
            .Where(r => ids.Contains(r.Id) && !_goneBeforeDescribe.Contains(r.Id))
            .ToDictionary(r => r.Id, r => new RecordSummary
            {
                Key = r.Key,
                Name = r.Name,
                ParentKey = r.ParentKey,
                ParentName = r.ParentName,
                StatusName = r.Lifecycle.ToString(),
                Lifecycle = r.Lifecycle,
                Health = r.Health,
                // The record's own id stands in for its current health check's and its parent's.
                HealthCheckId = r.Health is null ? null : r.Id,
                ParentId = r.ParentKey is null ? null : r.Id,
                People = [new RecordPerson("Owner", "Avery Chen", null)],
            });

        return Task.FromResult(summaries);
    }

    public Task<IQueryable<ActivityStamp>> VisibleActivity(IQueryable<ActivityStamp> activity, CancellationToken cancellationToken)
    {
        var hidden = HiddenEventTypes.ToArray();
        return Task.FromResult(activity.Where(a => !hidden.Contains(a.EventType)));
    }

    public Task<IQueryable<Notification>> VisibleNotifications(IQueryable<Notification> notifications, CancellationToken cancellationToken)
    {
        var hidden = HiddenNotificationEvents.ToArray();
        return Task.FromResult(notifications.Where(n => !hidden.Contains(n.Event)));
    }

    public Task<IReadOnlySet<Guid>> ActorHiddenOn(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlySet<Guid>>(ids.Where(ActorHiddenRecords.Contains).ToHashSet());

    public async Task<PagedResponse<ActivityLogDto>> ReadActivity(
        Guid id,
        IActivityLogReader activityLogReader,
        PageRequest paging,
        CancellationToken cancellationToken)
    {
        var page = await activityLogReader.Read(id, ActivityAggregateType, paging, cancellationToken);
        return page with { Items = [.. page.Items.Where(a => !HiddenEventTypes.Contains(a.EventType))] };
    }
}

/// <summary>A record of a <see cref="FakeRecordReadRule"/>.</summary>
public sealed record FakeRecord(
    Guid Id,
    string Key,
    string Name,
    string? ParentKey,
    string? ParentName,
    HealthStatus? Health,
    LifecycleCategory Lifecycle);
