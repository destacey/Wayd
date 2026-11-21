using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Kpis;

namespace Wayd.Common.Application.Kpis;

/// <summary>
/// The one implementation of <see cref="IKpiWriter"/>. Registered by Infrastructure; public because Wolverine's
/// generated handlers construct it.
/// </summary>
public sealed class KpiWriter(IKpisDbContext dbContext, IDateTimeProvider dateTimeProvider) : IKpiWriter
{
    private readonly IKpisDbContext _dbContext = dbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    /// <inheritdoc/>
    public async Task RemoveAll(string subjectType, Guid subjectId, EventActor actor, CancellationToken cancellationToken)
    {
        var descriptor = KpiSubjectTypes.Resolve(subjectType);
        if (descriptor.IsFailure)
            throw new ArgumentException(descriptor.Error, nameof(subjectType));

        var key = descriptor.Value.Key;
        var kpis = await _dbContext.Kpis
            .Where(k => k.SubjectType == key && k.SubjectId == subjectId)
            .ToListAsync(cancellationToken);

        var now = _dateTimeProvider.Now;
        foreach (var kpi in kpis)
        {
            kpi.Remove(actor, now);
            _dbContext.Kpis.Remove(kpi);
        }
    }
}
