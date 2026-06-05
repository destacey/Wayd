using Wayd.Common.Application.Persistence;
using Wayd.Common.Domain.Scoring;

namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record UpdateScoringModelOutputCommand(
    Guid ScoringModelId,
    Guid OutputId,
    string Name,
    string Token,
    string Formula,
    bool IsPrimary)
    : ICommand;

public sealed class UpdateScoringModelOutputCommandValidator : AbstractValidator<UpdateScoringModelOutputCommand>
{
    public UpdateScoringModelOutputCommandValidator()
    {
        RuleFor(x => x.ScoringModelId)
            .NotEmpty();

        RuleFor(x => x.OutputId)
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

internal sealed class UpdateScoringModelOutputCommandHandler(
    IWaydDbContext waydDbContext,
    ILogger<UpdateScoringModelOutputCommandHandler> logger)
    : ICommandHandler<UpdateScoringModelOutputCommand>
{
    private const string AppRequestName = nameof(UpdateScoringModelOutputCommand);

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<UpdateScoringModelOutputCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateScoringModelOutputCommand request, CancellationToken cancellationToken)
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
                return Result.Failure("Scoring Model not found.");
            }

            var updateResult = model.UpdateOutput(request.OutputId, request.Name, request.Token, request.Formula, request.IsPrimary);
            if (updateResult.IsFailure)
            {
                _logger.LogError("Unable to update output on Scoring Model {ScoringModelId}.  Error message: {Error}", request.ScoringModelId, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Output {OutputId} updated on Scoring Model {ScoringModelId}.", request.OutputId, request.ScoringModelId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
