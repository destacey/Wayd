using Wayd.Common.Application.StatusWorkflows.Dtos;
using Wayd.ProductManagement.Domain;

namespace Wayd.ProductManagement.Application.Products.Queries;

/// <summary>
/// The statuses a product can be moved to, from the workflow governing it.
/// </summary>
/// <remarks>
/// Statuses are configurable, so a caller cannot hold a fixed list — and moving a product to a status
/// from some other workflow is refused. This is how a picker learns what is actually reachable.
/// <para>
/// Ordered as the workflow orders them, not alphabetically: the sequence is how an administrator laid
/// the lifecycle out, and re-sorting would lose that.
/// </para>
/// </remarks>
public sealed record GetProductStatusOptionsQuery
    : IQuery<IReadOnlyCollection<StatusNavigationDto>>;

public sealed class GetProductStatusOptionsQueryHandler(IStatusResolver statusResolver)
    : IQueryHandler<GetProductStatusOptionsQuery, IReadOnlyCollection<StatusNavigationDto>>
{
    private readonly IStatusResolver _statusResolver = statusResolver;

    public async Task<IReadOnlyCollection<StatusNavigationDto>> Handle(
        GetProductStatusOptionsQuery query, CancellationToken cancellationToken)
    {
        var workflow = await _statusResolver.ForScope(
            ProductWorkflowOwners.Product.Key, scopeId: null, cancellationToken);

        // An empty list rather than a failure: a misconfigured workflow is an administrator's problem,
        // and the command that follows reports it with the reason. A picker with nothing in it says
        // the same thing without needing this query to carry an error shape.
        if (workflow.IsFailure)
        {
            return [];
        }

        return
        [
            .. workflow.Value.Statuses.Select(s => new StatusNavigationDto
            {
                Id = s.Id,
                Name = s.Name,
                Category = s.Category,
                Alias = s.Alias,
            }),
        ];
    }
}
