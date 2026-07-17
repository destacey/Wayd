using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Scoring;

namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record AddScoringModelOutputCommand(
    Guid ScoringModelId,
    string Name,
    string Token,
    string Formula,
    bool IsPrimary)
    : ICommand<Guid>;

public sealed class AddScoringModelOutputCommandValidator : AbstractValidator<AddScoringModelOutputCommand>
{
    public AddScoringModelOutputCommandValidator()
    {
        RuleFor(x => x.ScoringModelId)
            .NotEmpty();

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

public sealed class AddScoringModelOutputCommandHandler(
    IWaydDbContext waydDbContext,
    ILogger<AddScoringModelOutputCommandHandler> logger)
    : ICommandHandler<AddScoringModelOutputCommand, Guid>
{
    private const string AppRequestName = nameof(AddScoringModelOutputCommand);

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<AddScoringModelOutputCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(AddScoringModelOutputCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _waydDbContext.ScoringModels
                .Include(x => x.Criteria)
                .Include(x => x.Outputs)
                .FirstOrDefaultAsync(r => r.Id == request.ScoringModelId, cancellationToken);
            if (model is null)
            {
                _logger.LogInformation("Scoring Model {ScoringModelId} not found.", request.ScoringModelId);
                return Result.Failure<Guid>("Scoring Model not found.");
            }

            var addResult = model.AddOutput(request.Name, request.Token, request.Formula, request.IsPrimary);
            if (addResult.IsFailure)
            {
                _logger.LogError("Unable to add output to Scoring Model {ScoringModelId}.  Error message: {Error}", request.ScoringModelId, addResult.Error);
                return Result.Failure<Guid>(addResult.Error);
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Output {OutputId} added to Scoring Model {ScoringModelId}.", addResult.Value.Id, request.ScoringModelId);

            return Result.Success(addResult.Value.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}
