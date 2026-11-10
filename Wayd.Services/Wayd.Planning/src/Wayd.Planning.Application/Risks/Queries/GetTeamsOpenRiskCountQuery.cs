using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Planning.Application.Risks.Queries;

/// <summary>
/// How many open risks each of <paramref name="TeamIds"/> has, keyed by team id: the risks a team's Risks section
/// lists by default. A team with none is missing.
/// </summary>
public sealed record GetTeamsOpenRiskCountQuery(IReadOnlyCollection<Guid> TeamIds) : IQuery<IReadOnlyDictionary<Guid, int>>;

/// <summary>Handles <see cref="GetTeamsOpenRiskCountQuery"/>.</summary>
public sealed class GetTeamsOpenRiskCountQueryHandler(IPlanningDbContext planningDbContext)
    : IQueryHandler<GetTeamsOpenRiskCountQuery, IReadOnlyDictionary<Guid, int>>
{
    private readonly IPlanningDbContext _planningDbContext = planningDbContext;

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<Guid, int>> Handle(GetTeamsOpenRiskCountQuery request, CancellationToken cancellationToken)
    {
        var teamIds = request.TeamIds.Distinct().ToList();
        if (teamIds.Count == 0)
            return new Dictionary<Guid, int>();

        return await _planningDbContext.Risks
            .Where(r => r.TeamId.HasValue && teamIds.Contains(r.TeamId.Value) && r.Status != RiskStatus.Closed)
            .GroupBy(r => r.TeamId!.Value)
            .Select(g => new { TeamId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.TeamId, g => g.Count, cancellationToken);
    }
}
