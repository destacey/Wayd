using Wayd.Common.Application.Persistence;
namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record ReorderScoringModelCriteriaCommand(
    Guid ScoringModelId,
    List<Guid> OrderedCriterionIds)
    : ICommand;

public sealed class ReorderScoringModelCriteriaCommandValidator : AbstractValidator<ReorderScoringModelCriteriaCommand>
{
    public ReorderScoringModelCriteriaCommandValidator()
    {
        RuleFor(x => x.ScoringModelId)
            .NotEmpty();

        RuleFor(x => x.OrderedCriterionIds)
            .NotEmpty();
    }
}

public sealed class ReorderScoringModelCriteriaCommandHandler(
    IWaydDbContext waydDbContext,
    ILogger<ReorderScoringModelCriteriaCommandHandler> logger)
    : ICommandHandler<ReorderScoringModelCriteriaCommand>
{
    private const string AppRequestName = nameof(ReorderScoringModelCriteriaCommand);

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<ReorderScoringModelCriteriaCommandHandler> _logger = logger;

    public async Task<Result> Handle(ReorderScoringModelCriteriaCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _waydDbContext.ScoringModels
                .Include(x => x.Criteria)
                .FirstOrDefaultAsync(r => r.Id == request.ScoringModelId, cancellationToken);
            if (model is null)
            {
                _logger.LogInformation("Scoring Model {ScoringModelId} not found.", request.ScoringModelId);
                return Result.Failure("Scoring Model not found.");
            }

            var reorderResult = model.ReorderCriteria(request.OrderedCriterionIds);
            if (reorderResult.IsFailure)
            {
                _logger.LogError("Unable to reorder criteria on Scoring Model {ScoringModelId}.  Error message: {Error}", request.ScoringModelId, reorderResult.Error);
                return Result.Failure(reorderResult.Error);
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Criteria reordered on Scoring Model {ScoringModelId}.", request.ScoringModelId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
