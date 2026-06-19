using Wayd.Common.Application.Persistence;

namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record AddScoringScaleCommand(
    Guid ScoringModelId,
    string Name)
    : ICommand<Guid>;

public sealed class AddScoringScaleCommandValidator : AbstractValidator<AddScoringScaleCommand>
{
    public AddScoringScaleCommandValidator()
    {
        RuleFor(x => x.ScoringModelId)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(64);
    }
}

internal sealed class AddScoringScaleCommandHandler(
    IWaydDbContext waydDbContext,
    ILogger<AddScoringScaleCommandHandler> logger)
    : ICommandHandler<AddScoringScaleCommand, Guid>
{
    private const string AppRequestName = nameof(AddScoringScaleCommand);

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<AddScoringScaleCommandHandler> _logger = logger;

    public async Task<Result<Guid>> Handle(AddScoringScaleCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _waydDbContext.ScoringModels
                .Include(x => x.Scales)
                .FirstOrDefaultAsync(r => r.Id == request.ScoringModelId, cancellationToken);
            if (model is null)
            {
                _logger.LogInformation("Scoring Model {ScoringModelId} not found.", request.ScoringModelId);
                return Result.Failure<Guid>("Scoring Model not found.");
            }

            var addResult = model.AddScale(request.Name);
            if (addResult.IsFailure)
            {
                _logger.LogError("Unable to add scale to Scoring Model {ScoringModelId}.  Error message: {Error}", request.ScoringModelId, addResult.Error);
                return Result.Failure<Guid>(addResult.Error);
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Scale {ScaleId} added to Scoring Model {ScoringModelId}.", addResult.Value.Id, request.ScoringModelId);

            return Result.Success(addResult.Value.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<Guid>($"Error handling {AppRequestName} command.");
        }
    }
}
