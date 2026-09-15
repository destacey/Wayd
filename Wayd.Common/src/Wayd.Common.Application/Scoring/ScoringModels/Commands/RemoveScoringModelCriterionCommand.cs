using Wayd.Common.Application.Persistence;
namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record RemoveScoringModelCriterionCommand(
    Guid ScoringModelId,
    Guid CriterionId)
    : ICommand;

public sealed class RemoveScoringModelCriterionCommandValidator : AbstractValidator<RemoveScoringModelCriterionCommand>
{
    public RemoveScoringModelCriterionCommandValidator()
    {
        RuleFor(x => x.ScoringModelId)
            .NotEmpty();

        RuleFor(x => x.CriterionId)
            .NotEmpty();
    }
}

public sealed class RemoveScoringModelCriterionCommandHandler(
    IWaydDbContext waydDbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<RemoveScoringModelCriterionCommandHandler> logger)
    : ICommandHandler<RemoveScoringModelCriterionCommand>
{
    private const string AppRequestName = nameof(RemoveScoringModelCriterionCommand);

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<RemoveScoringModelCriterionCommandHandler> _logger = logger;

    public async Task<Result> Handle(RemoveScoringModelCriterionCommand request, CancellationToken cancellationToken)
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

            var removeResult = model.RemoveCriterion(request.CriterionId, EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()), _dateTimeProvider.Now);
            if (removeResult.IsFailure)
            {
                _logger.LogError("Unable to remove criterion from Scoring Model {ScoringModelId}.  Error message: {Error}", request.ScoringModelId, removeResult.Error);
                return Result.Failure(removeResult.Error);
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Criterion {CriterionId} removed from Scoring Model {ScoringModelId}.", request.CriterionId, request.ScoringModelId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
