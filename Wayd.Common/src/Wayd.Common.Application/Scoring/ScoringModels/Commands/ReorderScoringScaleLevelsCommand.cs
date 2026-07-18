using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record ReorderScoringScaleLevelsCommand(
    Guid ScoringModelId,
    Guid ScaleId,
    List<Guid> OrderedLevelIds)
    : ICommand;

public sealed class ReorderScoringScaleLevelsCommandValidator : AbstractValidator<ReorderScoringScaleLevelsCommand>
{
    public ReorderScoringScaleLevelsCommandValidator()
    {
        RuleFor(x => x.ScoringModelId)
            .NotEmpty();

        RuleFor(x => x.ScaleId)
            .NotEmpty();

        RuleFor(x => x.OrderedLevelIds)
            .NotEmpty();
    }
}

public sealed class ReorderScoringScaleLevelsCommandHandler(
    IWaydDbContext waydDbContext,
    ILogger<ReorderScoringScaleLevelsCommandHandler> logger)
    : ICommandHandler<ReorderScoringScaleLevelsCommand>
{
    private const string AppRequestName = nameof(ReorderScoringScaleLevelsCommand);

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<ReorderScoringScaleLevelsCommandHandler> _logger = logger;

    public async Task<Result> Handle(ReorderScoringScaleLevelsCommand request, CancellationToken cancellationToken)
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

            var reorderResult = model.ReorderScaleLevels(request.ScaleId, request.OrderedLevelIds);
            if (reorderResult.IsFailure)
            {
                _logger.LogError("Unable to reorder rating levels on Scoring Model {ScoringModelId}.  Error message: {Error}", request.ScoringModelId, reorderResult.Error);
                return Result.Failure(reorderResult.Error);
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Rating levels reordered on scale {ScaleId} on Scoring Model {ScoringModelId}.", request.ScaleId, request.ScoringModelId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
