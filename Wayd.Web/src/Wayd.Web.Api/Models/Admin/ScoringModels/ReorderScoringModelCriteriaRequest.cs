using Wayd.Common.Application.Scoring.ScoringModels.Commands;

namespace Wayd.Web.Api.Models.Admin.ScoringModels;

public sealed record ReorderScoringModelCriteriaRequest
{
    /// <summary>
    /// The ordered list of criterion IDs representing the desired order.
    /// </summary>
    public List<Guid> OrderedCriterionIds { get; set; } = [];

    public ReorderScoringModelCriteriaCommand ToReorderScoringModelCriteriaCommand(Guid scoringModelId)
    {
        return new ReorderScoringModelCriteriaCommand(scoringModelId, OrderedCriterionIds);
    }
}

public sealed class ReorderScoringModelCriteriaRequestValidator : AbstractValidator<ReorderScoringModelCriteriaRequest>
{
    public ReorderScoringModelCriteriaRequestValidator()
    {
        RuleFor(x => x.OrderedCriterionIds)
            .NotEmpty();
    }
}
