using Wayd.Common.Domain.Enums.Planning;

namespace Wayd.Common.Application.Requests.Planning.Queries;

/// <summary>
/// The category of the planning interval iteration each sprint is mapped to, keyed by sprint id. A sprint maps
/// to at most one iteration; one that is not mapped, or only to a deleted planning interval, is missing from the
/// result.
/// </summary>
public sealed record GetSprintIterationCategoriesQuery(IReadOnlyCollection<Guid> SprintIds) : IQuery<IReadOnlyDictionary<Guid, IterationCategory>>;
