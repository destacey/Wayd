using Wayd.Common.Application.Scoring.ScoringModels.Queries;

namespace Wayd.Web.Api.Models.Admin.ScoringModels;

/// <summary>
/// A request to evaluate (test) a scoring model against a set of supplied criterion values, returning
/// the resulting output values without persisting anything.
/// </summary>
public sealed record EvaluateScoringModelRequest
{
    /// <summary>
    /// The value to plug in for each criterion, keyed by criterion id.
    /// </summary>
    public List<CriterionValue> CriterionValues { get; set; } = [];

    public EvaluateScoringModelQuery ToQuery(Guid scoringModelId)
    {
        return new EvaluateScoringModelQuery(
            scoringModelId,
            CriterionValues
                .Select(cv => new EvaluateScoringModelQuery.CriterionValue(cv.CriterionId, cv.Value))
                .ToList());
    }

    public sealed record CriterionValue
    {
        public Guid CriterionId { get; set; }
        public decimal Value { get; set; }
    }
}

public sealed class EvaluateScoringModelRequestValidator : AbstractValidator<EvaluateScoringModelRequest>
{
    public EvaluateScoringModelRequestValidator()
    {
        RuleForEach(x => x.CriterionValues).ChildRules(cv =>
            cv.RuleFor(c => c.CriterionId).NotEmpty());
    }
}
