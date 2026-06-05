using Wayd.Common.Domain.Scoring;
using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record CreateScoringModelCommand(
    string Name,
    string Description,
    List<CreateScoringModelCommand.ScaleInput>? Scales,
    List<CreateScoringModelCommand.CriterionInput>? Criteria,
    List<CreateScoringModelCommand.OutputInput>? Outputs)
    : ICommand<Guid>
{
    public sealed record ScaleInput(string Name, List<ScaleLevelInput> Levels);
    public sealed record ScaleLevelInput(string Label, decimal Value);
    public sealed record CriterionInput(string Name, string Token, string? Description, decimal? Weight, string? ScaleName);
    public sealed record OutputInput(string Name, string Token, string Formula, bool IsPrimary);
}

public sealed class CreateScoringModelCommandValidator : AbstractValidator<CreateScoringModelCommand>
{
    public CreateScoringModelCommandValidator()
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

internal sealed class CreateScoringModelCommandHandler(
    IWaydDbContext waydDbContext,
    ILogger<CreateScoringModelCommandHandler> logger)
    : ICommandHandler<CreateScoringModelCommand, Guid>
{
    private const string AppRequestName = nameof(CreateScoringModelCommand);

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<CreateScoringModelCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(CreateScoringModelCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var scales = request.Scales?
                .Select(s => (s.Name, (IEnumerable<(string Label, decimal Value)>)s.Levels
                    .Select(l => (l.Label, l.Value))
                    .ToList()))
                .ToList();

            var criteria = request.Criteria?
                .Select(c => (c.Name, c.Token, c.Description, c.Weight, c.ScaleName))
                .ToList();

            var outputs = request.Outputs?
                .Select(o => (o.Name, o.Token, o.Formula, o.IsPrimary))
                .ToList();

            var model = ScoringModel.Create(
                request.Name,
                request.Description,
                scales,
                criteria,
                outputs
                );

            await _waydDbContext.ScoringModels.AddAsync(model, cancellationToken);
            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Scoring Model {ScoringModelId} created.", model.Id);

            return Result.Success(model.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}
