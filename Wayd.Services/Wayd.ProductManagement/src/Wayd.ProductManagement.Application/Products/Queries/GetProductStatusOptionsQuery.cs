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
/// <param name="ProductId">
/// A product whose allowed moves to return: the statuses its workflow lets it move to from its current
/// status, excluding that status, each with what the move needs. Without it, every status in the
/// workflow, with no conditions.
/// </param>
public sealed record GetProductStatusOptionsQuery(Guid? ProductId = null)
    : IQuery<IReadOnlyCollection<StatusOptionDto>?>;

public sealed class GetProductStatusOptionsQueryHandler(
    IProductManagementDbContext productManagementDbContext,
    IStatusResolver statusResolver)
    : IQueryHandler<GetProductStatusOptionsQuery, IReadOnlyCollection<StatusOptionDto>?>
{
    private readonly IProductManagementDbContext _productManagementDbContext = productManagementDbContext;
    private readonly IStatusResolver _statusResolver = statusResolver;

    /// <returns><c>null</c> when a product was named and does not exist.</returns>
    public async Task<IReadOnlyCollection<StatusOptionDto>?> Handle(
        GetProductStatusOptionsQuery query, CancellationToken cancellationToken)
    {
        Guid? currentStatusId = null;
        if (query.ProductId is { } productId)
        {
            currentStatusId = await _productManagementDbContext.Products
                .Where(p => p.Id == productId)
                .Select(p => (Guid?)p.StatusId)
                .FirstOrDefaultAsync(cancellationToken);

            if (currentStatusId is null)
            {
                return null;
            }
        }

        var workflow = await _statusResolver.ForScope(
            ProductWorkflowOwners.Product.Key, scopeId: null, cancellationToken);

        // An empty list rather than a failure: a misconfigured workflow is an administrator's problem,
        // and the command that follows reports it with the reason. A picker with nothing in it says
        // the same thing without needing this query to carry an error shape.
        if (workflow.IsFailure)
        {
            return [];
        }

        var statuses = currentStatusId is { } fromStatusId
            ? workflow.Value.AllowedTargets(fromStatusId)
            : workflow.Value.Statuses;

        return
        [
            .. statuses.Select(s => new StatusOptionDto
            {
                Id = s.Id,
                Name = s.Name,
                Category = s.Category,
                CategoryLabel = ProductWorkflowOwners.Product.CategoryLabel(s.Category),
                Alias = s.Alias,
                Conditions = currentStatusId is { } from ? workflow.Value.ConditionsFor(from, s.Id) : [],
            }),
        ];
    }
}
