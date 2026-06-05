using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record AddScoringScaleLevelCommand(
    Guid ScoringModelId,
    Guid ScaleId,
    string Label,
    decimal Value)
    : ICommand<Guid>;

public sealed class AddScoringScaleLevelCommandValidator : AbstractValidator<AddScoringScaleLevelCommand>
{
    public AddScoringScaleLevelCommandValidator()
    {
        RuleFor(x => x.ScoringModelId)
            .NotEmpty();

        RuleFor(x => x.ScaleId)
            .NotEmpty();

        RuleFor(x => x.Label)
            .NotEmpty()
            .MaximumLength(64);
    }
}

internal sealed class AddScoringScaleLevelCommandHandler(
    IWaydDbContext waydDbContext,
    ILogger<AddScoringScaleLevelCommandHandler> logger)
    : ICommandHandler<AddScoringScaleLevelCommand, Guid>
{
    private const string AppRequestName = nameof(AddScoringScaleLevelCommand);

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<AddScoringScaleLevelCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(AddScoringScaleLevelCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _waydDbContext.ScoringModels
                .Include(x => x.Scales).ThenInclude(s => s.Levels)
                .FirstOrDefaultAsync(r => r.Id == request.ScoringModelId, cancellationToken);
            if (model is null)
            {
                _logger.LogInformation("Scoring Model {ScoringModelId} not found.", request.ScoringModelId);
                return Result.Failure<Guid>("Scoring Model not found.");
            }

            var addResult = model.AddScaleLevel(request.ScaleId, request.Label, request.Value);
            if (addResult.IsFailure)
            {
                _logger.LogError("Unable to add rating level to Scoring Model {ScoringModelId}.  Error message: {Error}", request.ScoringModelId, addResult.Error);
                return Result.Failure<Guid>(addResult.Error);
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Rating level {LevelId} added to scale {ScaleId} on Scoring Model {ScoringModelId}.", addResult.Value.Id, request.ScaleId, request.ScoringModelId);

            return Result.Success(addResult.Value.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}
