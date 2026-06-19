using Wayd.Common.Application.Scoring.ScoringModels.Commands;
using Wayd.Common.Domain.Scoring;

namespace Wayd.Web.Api.Models.Admin.ScoringModels;

public sealed record ScoringModelOutputRequest
{
    /// <summary>
    /// The name of the output (e.g., "Cost of Delay", "WSJF").
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The token other output formulas can reference (e.g., "CoD").
    /// </summary>
    public string Token { get; set; } = default!;

    /// <summary>
    /// The arithmetic formula over criterion and earlier-output tokens.
    /// </summary>
    public string Formula { get; set; } = default!;

    /// <summary>
    /// Whether this output is the model's primary score.
    /// </summary>
    public bool IsPrimary { get; set; }

    public AddScoringModelOutputCommand ToAddCommand(Guid scoringModelId)
    {
        return new AddScoringModelOutputCommand(scoringModelId, Name, Token, Formula, IsPrimary);
    }

    public UpdateScoringModelOutputCommand ToUpdateCommand(Guid scoringModelId, Guid outputId)
    {
        return new UpdateScoringModelOutputCommand(scoringModelId, outputId, Name, Token, Formula, IsPrimary);
    }
}

public sealed class ScoringModelOutputRequestValidator : AbstractValidator<ScoringModelOutputRequest>
{
    public ScoringModelOutputRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Token)
            .NotEmpty()
            .MaximumLength(ScoringToken.MaxLength)
            .Must(ScoringToken.IsValid)
            .WithMessage("'{PropertyValue}' is not a valid token.");

        RuleFor(x => x.Formula)
            .NotEmpty()
            .MaximumLength(ScoringFormulaEvaluator.MaxFormulaLength);
    }
}
