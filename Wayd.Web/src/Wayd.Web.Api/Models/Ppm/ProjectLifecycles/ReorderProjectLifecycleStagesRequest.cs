using Wayd.ProjectPortfolioManagement.Application.ProjectLifecycles.Commands;

namespace Wayd.Web.Api.Models.Ppm.ProjectLifecycles;

public sealed record ReorderProjectLifecycleStagesRequest
{
    /// <summary>
    /// The ordered list of stage IDs representing the desired order.
    /// </summary>
    public List<Guid> OrderedStageIds { get; set; } = [];

    public ReorderProjectLifecycleStagesCommand ToReorderProjectLifecycleStagesCommand(Guid lifecycleId)
    {
        return new ReorderProjectLifecycleStagesCommand(lifecycleId, OrderedStageIds);
    }
}

public sealed class ReorderProjectLifecycleStagesRequestValidator : AbstractValidator<ReorderProjectLifecycleStagesRequest>
{
    public ReorderProjectLifecycleStagesRequestValidator()
    {
        RuleFor(x => x.OrderedStageIds)
            .NotEmpty();
    }
}
