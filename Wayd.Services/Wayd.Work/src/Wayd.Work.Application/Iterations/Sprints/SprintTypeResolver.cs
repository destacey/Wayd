using Wayd.Common.Application.Requests.Planning.Queries;
using Wayd.Work.Domain.Models;

namespace Wayd.Work.Application.Iterations.Sprints;

internal static class SprintTypeResolver
{
    /// <summary>
    /// Works out each sprint's type, keyed by id, asking Planning once for the categories of the iterations
    /// they are mapped to.
    /// </summary>
    public static async Task<IReadOnlyDictionary<Guid, ResolvedSprintType>> ResolveSprintTypes(this IDispatcher dispatcher,
        IReadOnlyCollection<Iteration> sprints, CancellationToken cancellationToken)
    {
        if (sprints.Count == 0)
            return new Dictionary<Guid, ResolvedSprintType>();

        var categories = await dispatcher.Send(new GetSprintIterationCategoriesQuery([.. sprints.Select(s => s.Id)]), cancellationToken);

        return sprints.ToDictionary(
            s => s.Id,
            s => s.ResolveSprintType(categories.TryGetValue(s.Id, out var category) ? category : null));
    }
}
