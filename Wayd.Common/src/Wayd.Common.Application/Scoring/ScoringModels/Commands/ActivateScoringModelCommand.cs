using Wayd.Common.Application.Persistence;
namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record ActivateScoringModelCommand(Guid Id) : ICommand;

public sealed class ActivateScoringModelCommandValidator : AbstractValidator<ActivateScoringModelCommand>
{
    public ActivateScoringModelCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class ActivateScoringModelCommandHandler(
    IWaydDbContext waydDbContext,
    ILogger<ActivateScoringModelCommandHandler> logger)
    : ICommandHandler<ActivateScoringModelCommand>
{
    private const string AppRequestName = nameof(ActivateScoringModelCommand);
    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<ActivateScoringModelCommandHandler> _logger = logger;

    public async Task<Result> Handle(ActivateScoringModelCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _waydDbContext.ScoringModels
                .Include(x => x.Criteria)
                .Include(x => x.Scales).ThenInclude(s => s.Levels)
                .Include(x => x.Outputs)
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (model is null)
            {
                _logger.LogInformation("Scoring Model {ScoringModelId} not found.", request.Id);
                return Result.Failure("Scoring Model not found.");
            }

            var activateResult = model.Activate();
            if (activateResult.IsFailure)
            {
                // Reset the entity
                await _waydDbContext.Entry(model).ReloadAsync(cancellationToken);
                model.ClearDomainEvents();

                _logger.LogError("Unable to activate Scoring Model {ScoringModelId}.  Error message: {Error}", request.Id, activateResult.Error);

                return Result.Failure(activateResult.Error);
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Scoring Model {ScoringModelId} activated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
