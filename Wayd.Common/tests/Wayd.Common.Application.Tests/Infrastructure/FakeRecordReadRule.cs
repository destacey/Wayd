using Wayd.Common.Application.Records;
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

    public FakeRecord Add(string key, string name, string? parentKey = null, string? parentName = null)
    {
        var record = new FakeRecord(Guid.NewGuid(), key, name, parentKey, parentName);
        _records.Add(record);
        return record;
    }

    /// <summary>Makes one record unreadable to the caller while it still exists, as a row-level rule would.</summary>
    public void Hide(Guid id) => _hidden.Add(id);

    /// <summary>Deletes a record, leaving any follows of it behind.</summary>
    public void Delete(Guid id) => _records.RemoveAll(r => r.Id == id);

    public Task<IQueryable<Guid>?> ReadableIds(CancellationToken cancellationToken)
    {
        IQueryable<Guid>? readable = CanView
            ? _records.AsDbSet().Where(r => !_hidden.Contains(r.Id)).Select(r => r.Id)
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
            .ToDictionary(r => r.Id, r => new RecordSummary(r.Key, r.Name, r.ParentKey, r.ParentName));

        return Task.FromResult(summaries);
    }
}

/// <summary>A record of a <see cref="FakeRecordReadRule"/>.</summary>
public sealed record FakeRecord(Guid Id, string Key, string Name, string? ParentKey, string? ParentName);
