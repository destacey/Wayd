using NodaTime;
using Wayd.Common.Domain.Enums.Work;
using Wayd.Work.Domain.Models.SprintScope;

namespace Wayd.Work.Domain.Tests.Data;

/// <summary>
/// One work item's contiguous history for sprint scope and burn tests, each state holding until the next begins.
/// </summary>
public sealed class ScopeItemHistory
{
    private readonly List<SprintScopePeriod> _periods = [];

    public Guid WorkItemId { get; } = Guid.NewGuid();

    public IReadOnlyList<SprintScopePeriod> Periods => _periods;

    /// <summary>Closes the current state at <paramref name="from"/> and opens the next.</summary>
    public ScopeItemHistory Then(Instant from, Guid? iterationId, WorkStatusCategory status, double? storyPoints = 3, bool isRequirement = true)
    {
        if (_periods.Count > 0)
            _periods[^1] = _periods[^1] with { ValidTo = from };

        _periods.Add(new SprintScopePeriod(WorkItemId, from, null, iterationId, isRequirement, status, storyPoints, null, null));
        return this;
    }
}
