using Wayd.Common.Application.Scoring.ScoringModels.Commands;

namespace Wayd.Web.Api.Models.Admin.ScoringModels;

public sealed record ReorderScoringScalesRequest
{
    /// <summary>
    /// The ordered list of scale IDs representing the desired order.
    /// </summary>
    public List<Guid> OrderedScaleIds { get; set; } = [];

    public ReorderScoringScalesCommand ToReorderScoringScalesCommand(Guid scoringModelId)
    {
        return new ReorderScoringScalesCommand(scoringModelId, OrderedScaleIds);
    }
}

public sealed class ReorderScoringScalesRequestValidator : AbstractValidator<ReorderScoringScalesRequest>
{
    public ReorderScoringScalesRequestValidator()
    {
        RuleFor(x => x.OrderedScaleIds)
            .NotEmpty();
    }
}
