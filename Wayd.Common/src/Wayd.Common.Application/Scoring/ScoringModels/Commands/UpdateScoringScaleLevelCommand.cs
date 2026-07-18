using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record UpdateScoringScaleLevelCommand(
    Guid ScoringModelId,
    Guid ScaleId,
    Guid LevelId,
    string Label,
    decimal Value)
    : ICommand;

public sealed class UpdateScoringScaleLevelCommandValidator : AbstractValidator<UpdateScoringScaleLevelCommand>
{
    public UpdateScoringScaleLevelCommandValidator()
    {
        RuleFor(x => x.ScoringModelId)
            .NotEmpty();

        RuleFor(x => x.ScaleId)
            .NotEmpty();

        RuleFor(x => x.LevelId)
            .NotEmpty();

        RuleFor(x => x.Label)
            .NotEmpty()
            .MaximumLength(64);
    }
}

public sealed class UpdateScoringScaleLevelCommandHandler(
    IWaydDbContext waydDbContext,
    ILogger<UpdateScoringScaleLevelCommandHandler> logger)
    : ICommandHandler<UpdateScoringScaleLevelCommand>
{
    private const string AppRequestName = nameof(UpdateScoringScaleLevelCommand);

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<UpdateScoringScaleLevelCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateScoringScaleLevelCommand request, CancellationToken cancellationToken)
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

            var updateResult = model.UpdateScaleLevel(request.ScaleId, request.LevelId, request.Label, request.Value);
            if (updateResult.IsFailure)
            {
                _logger.LogError("Unable to update rating level on Scoring Model {ScoringModelId}.  Error message: {Error}", request.ScoringModelId, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Rating level {LevelId} updated on scale {ScaleId} on Scoring Model {ScoringModelId}.", request.LevelId, request.ScaleId, request.ScoringModelId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
