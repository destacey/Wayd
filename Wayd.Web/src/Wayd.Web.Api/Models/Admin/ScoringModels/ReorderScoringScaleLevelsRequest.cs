using Wayd.Common.Application.Scoring.ScoringModels.Commands;

namespace Wayd.Web.Api.Models.Admin.ScoringModels;

public sealed record ReorderScoringScaleLevelsRequest
{
    /// <summary>
    /// The ordered list of rating level IDs representing the desired order.
    /// </summary>
    public List<Guid> OrderedLevelIds { get; set; } = [];

    public ReorderScoringScaleLevelsCommand ToReorderScoringScaleLevelsCommand(Guid scoringModelId, Guid scaleId)
    {
        return new ReorderScoringScaleLevelsCommand(scoringModelId, scaleId, OrderedLevelIds);
    }
}

public sealed class ReorderScoringScaleLevelsRequestValidator : AbstractValidator<ReorderScoringScaleLevelsRequest>
{
    public ReorderScoringScaleLevelsRequestValidator()
    {
        RuleFor(x => x.OrderedLevelIds)
            .NotEmpty();
    }
}
