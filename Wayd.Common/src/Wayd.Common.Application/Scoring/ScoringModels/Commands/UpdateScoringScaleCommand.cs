using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record UpdateScoringScaleCommand(
    Guid ScoringModelId,
    Guid ScaleId,
    string Name)
    : ICommand;

public sealed class UpdateScoringScaleCommandValidator : AbstractValidator<UpdateScoringScaleCommand>
{
    public UpdateScoringScaleCommandValidator()
    {
        RuleFor(x => x.ScoringModelId)
            .NotEmpty();

        RuleFor(x => x.ScaleId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(64);
    }
}

internal sealed class UpdateScoringScaleCommandHandler(
    IWaydDbContext waydDbContext,
    ILogger<UpdateScoringScaleCommandHandler> logger)
    : ICommandHandler<UpdateScoringScaleCommand>
{
    private const string AppRequestName = nameof(UpdateScoringScaleCommand);

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<UpdateScoringScaleCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateScoringScaleCommand request, CancellationToken cancellationToken)
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

            var updateResult = model.UpdateScale(request.ScaleId, request.Name);
            if (updateResult.IsFailure)
            {
                _logger.LogError("Unable to update scale on Scoring Model {ScoringModelId}.  Error message: {Error}", request.ScoringModelId, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Scale {ScaleId} updated on Scoring Model {ScoringModelId}.", request.ScaleId, request.ScoringModelId);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
