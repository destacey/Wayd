using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record RemoveScoringScaleCommand(
    Guid ScoringModelId,
    Guid ScaleId)
    : ICommand;

public sealed class RemoveScoringScaleCommandValidator : AbstractValidator<RemoveScoringScaleCommand>
{
    public RemoveScoringScaleCommandValidator()
    {
        RuleFor(x => x.ScoringModelId)
            .NotEmpty();

        RuleFor(x => x.ScaleId)
            .NotEmpty();
    }
}

internal sealed class RemoveScoringScaleCommandHandler(
    IWaydDbContext waydDbContext,
    ILogger<RemoveScoringScaleCommandHandler> logger)
    : ICommandHandler<RemoveScoringScaleCommand>
{
    private const string AppRequestName = nameof(RemoveScoringScaleCommand);

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<RemoveScoringScaleCommandHandler> _logger = logger;

    public async Task<Result> Handle(RemoveScoringScaleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _waydDbContext.ScoringModels
                .Include(x => x.Scales)
                .Include(x => x.Criteria)
                .FirstOrDefaultAsync(r => r.Id == request.ScoringModelId, cancellationToken);
            if (model is null)
            {
                _logger.LogInformation("Scoring Model {ScoringModelId} not found.", request.ScoringModelId);
                return Result.Failure("Scoring Model not found.");
            }

            var removeResult = model.RemoveScale(request.ScaleId);
            if (removeResult.IsFailure)
            {
                _logger.LogError("Unable to remove scale from Scoring Model {ScoringModelId}.  Error message: {Error}", request.ScoringModelId, removeResult.Error);
                return Result.Failure(removeResult.Error);
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Scale {ScaleId} removed from Scoring Model {ScoringModelId}.", request.ScaleId, request.ScoringModelId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
