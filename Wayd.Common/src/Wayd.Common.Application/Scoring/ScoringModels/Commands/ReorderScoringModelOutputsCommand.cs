using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record ReorderScoringModelOutputsCommand(
    Guid ScoringModelId,
    List<Guid> OrderedOutputIds)
    : ICommand;

public sealed class ReorderScoringModelOutputsCommandValidator : AbstractValidator<ReorderScoringModelOutputsCommand>
{
    public ReorderScoringModelOutputsCommandValidator()
    {
        RuleFor(x => x.ScoringModelId)
            .NotEmpty();

        RuleFor(x => x.OrderedOutputIds)
            .NotEmpty();
    }
}

internal sealed class ReorderScoringModelOutputsCommandHandler(
    IWaydDbContext waydDbContext,
    ILogger<ReorderScoringModelOutputsCommandHandler> logger)
    : ICommandHandler<ReorderScoringModelOutputsCommand>
{
    private const string AppRequestName = nameof(ReorderScoringModelOutputsCommand);

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<ReorderScoringModelOutputsCommandHandler> _logger = logger;

    public async Task<Result> Handle(ReorderScoringModelOutputsCommand request, CancellationToken cancellationToken)
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

            var reorderResult = model.ReorderOutputs(request.OrderedOutputIds);
            if (reorderResult.IsFailure)
            {
                _logger.LogError("Unable to reorder outputs on Scoring Model {ScoringModelId}.  Error message: {Error}", request.ScoringModelId, reorderResult.Error);
                return Result.Failure(reorderResult.Error);
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Outputs reordered on Scoring Model {ScoringModelId}.", request.ScoringModelId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
