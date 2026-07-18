using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record RemoveScoringScaleLevelCommand(
    Guid ScoringModelId,
    Guid ScaleId,
    Guid LevelId)
    : ICommand;

public sealed class RemoveScoringScaleLevelCommandValidator : AbstractValidator<RemoveScoringScaleLevelCommand>
{
    public RemoveScoringScaleLevelCommandValidator()
    {
        RuleFor(x => x.ScoringModelId)
            .NotEmpty();

        RuleFor(x => x.ScaleId)
            .NotEmpty();

        RuleFor(x => x.LevelId)
            .NotEmpty();
    }
}

public sealed class RemoveScoringScaleLevelCommandHandler(
    IWaydDbContext waydDbContext,
    ILogger<RemoveScoringScaleLevelCommandHandler> logger)
    : ICommandHandler<RemoveScoringScaleLevelCommand>
{
    private const string AppRequestName = nameof(RemoveScoringScaleLevelCommand);

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<RemoveScoringScaleLevelCommandHandler> _logger = logger;

    public async Task<Result> Handle(RemoveScoringScaleLevelCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _waydDbContext.ScoringModels
                .Include(x => x.Scales).ThenInclude(s => s.Levels)
                .FirstOrDefaultAsync(r => r.Id == request.ScoringModelId, cancellationToken);
            if (model is null)
            {
                _logger.LogInformation("Scoring Model {ScoringModelId} not found.", request.ScoringModelId);
                return Result.Failure("Scoring Model not found.");
            }

            var removeResult = model.RemoveScaleLevel(request.ScaleId, request.LevelId);
            if (removeResult.IsFailure)
            {
                _logger.LogError("Unable to remove rating level from Scoring Model {ScoringModelId}.  Error message: {Error}", request.ScoringModelId, removeResult.Error);
                return Result.Failure(removeResult.Error);
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Rating level {LevelId} removed from scale {ScaleId} on Scoring Model {ScoringModelId}.", request.LevelId, request.ScaleId, request.ScoringModelId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
