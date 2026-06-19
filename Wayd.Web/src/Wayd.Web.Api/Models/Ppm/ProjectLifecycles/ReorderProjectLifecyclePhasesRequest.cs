using Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;

namespace Wayd.Web.Api.Models.Ppm.ProjectLifecycles;

public sealed record ReorderProjectLifecyclePhasesRequest
{
    /// <summary>
    /// The ordered list of phase IDs representing the desired order.
    /// </summary>
    public List<Guid> OrderedPhaseIds { get; set; } = [];

    public ReorderProjectLifecyclePhasesCommand ToReorderProjectLifecyclePhasesCommand(Guid lifecycleId)
    {
        return new ReorderProjectLifecyclePhasesCommand(lifecycleId, OrderedPhaseIds);
    }
}

public sealed class ReorderProjectLifecyclePhasesRequestValidator : AbstractValidator<ReorderProjectLifecyclePhasesRequest>
{
    public ReorderProjectLifecyclePhasesRequestValidator()
    {
        RuleFor(x => x.OrderedPhaseIds)
            .NotEmpty();
    }
}
