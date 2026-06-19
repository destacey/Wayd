using Wayd.Common.Application.Requests.Goals.Dtos;

namespace Wayd.Common.Application.Requests.Goals.Queries;

/// <summary>
/// Searches objectives by name. Returns matching objectives with their PlanId and OwnerId
/// for cross-service enrichment.
/// </summary>
public sealed record SearchObjectivesByNameQuery(string SearchTerm, int MaxResults)
    : IQuery<IReadOnlyList<ObjectiveSearchResultDto>>;
