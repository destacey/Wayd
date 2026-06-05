using Wayd.Common.Application.Scoring.ScoringModels.Commands;
using Wayd.Common.Domain.Scoring;

namespace Wayd.Web.Api.Models.Admin.ScoringModels;

public sealed record ScoringModelCriterionRequest
{
    /// <summary>
    /// The name of the criterion.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The token used to reference this criterion in output formulas (e.g., "BV").
    /// </summary>
    public string Token { get; set; } = default!;

    /// <summary>
    /// An optional description of the criterion.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// An optional, non-authoritative weight (used only by the weighted-formula scaffolder).
    /// </summary>
    public decimal? Weight { get; set; }

    /// <summary>
    /// The optional rating scale this criterion is rated against. Null = free numeric entry.
    /// </summary>
    public Guid? ScaleId { get; set; }

    public AddScoringModelCriterionCommand ToAddCommand(Guid scoringModelId)
    {
        return new AddScoringModelCriterionCommand(scoringModelId, Name, Token, Description, Weight, ScaleId);
    }

    public UpdateScoringModelCriterionCommand ToUpdateCommand(Guid scoringModelId, Guid criterionId)
    {
        return new UpdateScoringModelCriterionCommand(scoringModelId, criterionId, Name, Token, Description, Weight, ScaleId);
    }
}

public sealed class ScoringModelCriterionRequestValidator : AbstractValidator<ScoringModelCriterionRequest>
{
    public ScoringModelCriterionRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Token)
            .NotEmpty()
            .MaximumLength(ScoringToken.MaxLength)
            .Must(ScoringToken.IsValid)
            .WithMessage("'{PropertyValue}' is not a valid token.");

        RuleFor(x => x.Description)
            .MaximumLength(1024);
    }
}
