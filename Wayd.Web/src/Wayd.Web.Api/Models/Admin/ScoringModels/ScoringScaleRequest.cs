using Wayd.Common.Application.Scoring.ScoringModels.Commands;

namespace Wayd.Web.Api.Models.Admin.ScoringModels;

public sealed record ScoringScaleRequest
{
    /// <summary>
    /// The name of the scale (e.g., "Fibonacci", "Impact").
    /// </summary>
    public string Name { get; set; } = default!;

    public AddScoringScaleCommand ToAddCommand(Guid scoringModelId)
    {
        return new AddScoringScaleCommand(scoringModelId, Name);
    }

    public UpdateScoringScaleCommand ToUpdateCommand(Guid scoringModelId, Guid scaleId)
    {
        return new UpdateScoringScaleCommand(scoringModelId, scaleId, Name);
    }
}

public sealed class ScoringScaleRequestValidator : AbstractValidator<ScoringScaleRequest>
{
    public ScoringScaleRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(64);
    }
}
