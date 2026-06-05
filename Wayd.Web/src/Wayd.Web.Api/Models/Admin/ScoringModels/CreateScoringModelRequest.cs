using Wayd.Common.Application.Scoring.ScoringModels.Commands;
using Wayd.Common.Domain.Scoring;

namespace Wayd.Web.Api.Models.Admin.ScoringModels;

public sealed record CreateScoringModelRequest
{
    /// <summary>
    /// The name of the scoring model.
    /// </summary>
    public string Name { get; set; } = default!;

    /// <summary>
    /// The description of the scoring model.
    /// </summary>
    public string Description { get; set; } = default!;

    /// <summary>
    /// Optional initial rating scales for the scoring model.
    /// </summary>
    public List<ScaleInput>? Scales { get; set; }

    /// <summary>
    /// Optional initial criteria (rated inputs) for the scoring model.
    /// </summary>
    public List<CriterionInput>? Criteria { get; set; }

    /// <summary>
    /// Optional initial output formulas for the scoring model.
    /// </summary>
    public List<OutputInput>? Outputs { get; set; }

    public sealed record ScaleInput
    {
        /// <summary>The name of the scale (e.g., "Fibonacci", "Impact").</summary>
        public string Name { get; set; } = default!;

        /// <summary>The ordered rating levels of the scale.</summary>
        public List<ScaleLevelInput> Levels { get; set; } = [];
    }

    public sealed record ScaleLevelInput
    {
        /// <summary>The label for the rating level (e.g., "Very High").</summary>
        public string Label { get; set; } = default!;

        /// <summary>The numeric value a chosen rating contributes.</summary>
        public decimal Value { get; set; }
    }

    public sealed record CriterionInput
    {
        /// <summary>The name of the criterion.</summary>
        public string Name { get; set; } = default!;

        /// <summary>The token used to reference this criterion in formulas (e.g., "BV").</summary>
        public string Token { get; set; } = default!;

        /// <summary>An optional description of the criterion.</summary>
        public string? Description { get; set; }

        /// <summary>An optional, non-authoritative weight (used only by the weighted-formula scaffolder).</summary>
        public decimal? Weight { get; set; }

        /// <summary>The name of a scale (from Scales) this criterion is rated against, or null for free numeric entry.</summary>
        public string? ScaleName { get; set; }
    }

    public sealed record OutputInput
    {
        /// <summary>The name of the output (e.g., "Cost of Delay", "WSJF").</summary>
        public string Name { get; set; } = default!;

        /// <summary>The token other output formulas can reference (e.g., "CoD").</summary>
        public string Token { get; set; } = default!;

        /// <summary>The arithmetic formula over criterion and earlier-output tokens.</summary>
        public string Formula { get; set; } = default!;

        /// <summary>Whether this output is the model's primary score.</summary>
        public bool IsPrimary { get; set; }
    }

    public CreateScoringModelCommand ToCreateScoringModelCommand()
    {
        var scales = Scales?
            .Select(s => new CreateScoringModelCommand.ScaleInput(
                s.Name,
                s.Levels.Select(l => new CreateScoringModelCommand.ScaleLevelInput(l.Label, l.Value)).ToList()))
            .ToList();

        var criteria = Criteria?
            .Select(c => new CreateScoringModelCommand.CriterionInput(c.Name, c.Token, c.Description, c.Weight, c.ScaleName))
            .ToList();

        var outputs = Outputs?
            .Select(o => new CreateScoringModelCommand.OutputInput(o.Name, o.Token, o.Formula, o.IsPrimary))
            .ToList();

        return new CreateScoringModelCommand(Name, Description, scales, criteria, outputs);
    }
}

public sealed class CreateScoringModelRequestValidator : AbstractValidator<CreateScoringModelRequest>
{
    public CreateScoringModelRequestValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(1024);

        RuleForEach(x => x.Criteria).ChildRules(criterion =>
        {
            criterion.RuleFor(c => c.Name)
                .NotEmpty()
                .MaximumLength(128);

            criterion.RuleFor(c => c.Token)
                .NotEmpty()
                .MaximumLength(ScoringToken.MaxLength)
                .Must(ScoringToken.IsValid)
                .WithMessage("'{PropertyValue}' is not a valid token.");

            criterion.RuleFor(c => c.Description)
                .MaximumLength(1024);
        });

        RuleForEach(x => x.Scales).ChildRules(scale =>
        {
            scale.RuleFor(s => s.Name)
                .NotEmpty()
                .MaximumLength(64);

            scale.RuleForEach(s => s.Levels).ChildRules(level =>
            {
                level.RuleFor(l => l.Label)
                    .NotEmpty()
                    .MaximumLength(64);
            });
        });

        RuleForEach(x => x.Outputs).ChildRules(output =>
        {
            output.RuleFor(o => o.Name)
                .NotEmpty()
                .MaximumLength(128);

            output.RuleFor(o => o.Token)
                .NotEmpty()
                .MaximumLength(ScoringToken.MaxLength)
                .Must(ScoringToken.IsValid)
                .WithMessage("'{PropertyValue}' is not a valid token.");

            output.RuleFor(o => o.Formula)
                .NotEmpty()
                .MaximumLength(ScoringFormulaEvaluator.MaxFormulaLength);
        });
    }
}
