using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record RemoveScoringModelOutputCommand(
    Guid ScoringModelId,
    Guid OutputId)
    : ICommand;

public sealed class RemoveScoringModelOutputCommandValidator : AbstractValidator<RemoveScoringModelOutputCommand>
{
    public RemoveScoringModelOutputCommandValidator()
    {
        RuleFor(x => x.ScoringModelId)
            .NotEmpty();

        RuleFor(x => x.OutputId)
            .NotEmpty();
    }
}

internal sealed class RemoveScoringModelOutputCommandHandler(
    IWaydDbContext waydDbContext,
    ILogger<RemoveScoringModelOutputCommandHandler> logger)
    : ICommandHandler<RemoveScoringModelOutputCommand>
{
    private const string AppRequestName = nameof(RemoveScoringModelOutputCommand);

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<RemoveScoringModelOutputCommandHandler> _logger = logger;

    public async Task<Result> Handle(RemoveScoringModelOutputCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _waydDbContext.ScoringModels
                .Include(x => x.Outputs)
                .FirstOrDefaultAsync(r => r.Id == request.ScoringModelId, cancellationToken);
            if (model is null)
            {
                _logger.LogInformation("Scoring Model {ScoringModelId} not found.", request.ScoringModelId);
                return Result.Failure("Scoring Model not found.");
            }

            var removeResult = model.RemoveOutput(request.OutputId);
            if (removeResult.IsFailure)
            {
                _logger.LogError("Unable to remove output from Scoring Model {ScoringModelId}.  Error message: {Error}", request.ScoringModelId, removeResult.Error);
                return Result.Failure(removeResult.Error);
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Output {OutputId} removed from Scoring Model {ScoringModelId}.", request.OutputId, request.ScoringModelId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
