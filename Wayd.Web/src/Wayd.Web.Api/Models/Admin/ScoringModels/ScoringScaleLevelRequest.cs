using Wayd.Common.Application.Scoring.ScoringModels.Commands;

namespace Wayd.Web.Api.Models.Admin.ScoringModels;

public sealed record ScoringScaleLevelRequest
{
    /// <summary>
    /// The label for the rating level (e.g., "Very High").
    /// </summary>
    public string Label { get; set; } = default!;

    /// <summary>
    /// The numeric value a chosen rating contributes to the criterion.
    /// </summary>
    public decimal Value { get; set; }

    public AddScoringScaleLevelCommand ToAddCommand(Guid scoringModelId, Guid scaleId)
    {
        return new AddScoringScaleLevelCommand(scoringModelId, scaleId, Label, Value);
    }

    public UpdateScoringScaleLevelCommand ToUpdateCommand(Guid scoringModelId, Guid scaleId, Guid levelId)
    {
        return new UpdateScoringScaleLevelCommand(scoringModelId, scaleId, levelId, Label, Value);
    }
}

public sealed class ScoringScaleLevelRequestValidator : AbstractValidator<ScoringScaleLevelRequest>
{
    public ScoringScaleLevelRequestValidator()
    {
        RuleFor(x => x.Label)
            .NotEmpty()
            .MaximumLength(64);
    }
}
