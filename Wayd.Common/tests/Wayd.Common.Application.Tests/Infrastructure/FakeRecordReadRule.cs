using Wayd.Common.Application.Records;
using Wayd.Common.Domain.Enums;
using Wayd.Tests.Shared.Infrastructure;

namespace Wayd.Common.Application.Tests.Infrastructure;

/// <summary>
/// A followable owner type whose records and read rule a test sets directly.
/// </summary>
public sealed class FakeRecordReadRule(string ownerType) : IRecordReadRule
{
    private readonly List<FakeRecord> _records = [];
    private readonly HashSet<Guid> _hidden = [];

    public string OwnerType { get; } = ownerType;

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
        LifecycleCategory lifecycle = LifecycleCategory.Active)
    {
        var record = new FakeRecord(Guid.NewGuid(), key, name, parentKey, parentName, health, lifecycle);
        _records.Add(record);
        return record;
    }

    /// <summary>Makes one record unreadable to the caller while it still exists, as a row-level rule would.</summary>
    public void Hide(Guid id) => _hidden.Add(id);

    /// <summary>Deletes a record, leaving any follows of it behind.</summary>
    public void Delete(Guid id) => _records.RemoveAll(r => r.Id == id);

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
            .Where(r => ids.Contains(r.Id))
            .ToDictionary(r => r.Id, r => new RecordSummary
            {
                Key = r.Key,
                Name = r.Name,
                ParentKey = r.ParentKey,
                ParentName = r.ParentName,
                StatusName = r.Lifecycle.ToString(),
                Lifecycle = r.Lifecycle,
                Health = r.Health,
                People = [new RecordPerson("Owner", "Avery Chen", null)],
            });

        return Task.FromResult(summaries);
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
