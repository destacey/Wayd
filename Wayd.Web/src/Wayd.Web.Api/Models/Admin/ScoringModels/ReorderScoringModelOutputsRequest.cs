using Wayd.Common.Application.Scoring.ScoringModels.Commands;

namespace Wayd.Web.Api.Models.Admin.ScoringModels;

public sealed record ReorderScoringModelOutputsRequest
{
    /// <summary>
    /// The ordered list of output IDs representing the desired evaluation order.
    /// </summary>
    public List<Guid> OrderedOutputIds { get; set; } = [];

    public ReorderScoringModelOutputsCommand ToReorderScoringModelOutputsCommand(Guid scoringModelId)
    {
        return new ReorderScoringModelOutputsCommand(scoringModelId, OrderedOutputIds);
    }
}

public sealed class ReorderScoringModelOutputsRequestValidator : AbstractValidator<ReorderScoringModelOutputsRequest>
{
    public ReorderScoringModelOutputsRequestValidator()
    {
        RuleFor(x => x.OrderedOutputIds)
            .NotEmpty();
    }
}
