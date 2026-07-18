using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record ReorderScoringScalesCommand(
    Guid ScoringModelId,
    List<Guid> OrderedScaleIds)
    : ICommand;

public sealed class ReorderScoringScalesCommandValidator : AbstractValidator<ReorderScoringScalesCommand>
{
    public ReorderScoringScalesCommandValidator()
    {
        RuleFor(x => x.ScoringModelId)
            .NotEmpty();

        RuleFor(x => x.OrderedScaleIds)
            .NotEmpty();
    }
}

public sealed class ReorderScoringScalesCommandHandler(
    IWaydDbContext waydDbContext,
    ILogger<ReorderScoringScalesCommandHandler> logger)
    : ICommandHandler<ReorderScoringScalesCommand>
{
    private const string AppRequestName = nameof(ReorderScoringScalesCommand);

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<ReorderScoringScalesCommandHandler> _logger = logger;

    public async Task<Result> Handle(ReorderScoringScalesCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _waydDbContext.ScoringModels
                .Include(x => x.Scales)
                .FirstOrDefaultAsync(r => r.Id == request.ScoringModelId, cancellationToken);
            if (model is null)
            {
                _logger.LogInformation("Scoring Model {ScoringModelId} not found.", request.ScoringModelId);
                return Result.Failure("Scoring Model not found.");
            }

            var reorderResult = model.ReorderScales(request.OrderedScaleIds);
            if (reorderResult.IsFailure)
            {
                _logger.LogError("Unable to reorder scales on Scoring Model {ScoringModelId}.  Error message: {Error}", request.ScoringModelId, reorderResult.Error);
                return Result.Failure(reorderResult.Error);
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Scales reordered on Scoring Model {ScoringModelId}.", request.ScoringModelId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
