using Wayd.Common.Application.Persistence;
namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record UpdateScoringModelCommand(
    Guid Id,
    string Name,
    string Description)
    : ICommand;

public sealed class UpdateScoringModelCommandValidator : AbstractValidator<UpdateScoringModelCommand>
{
    public UpdateScoringModelCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(128);

        RuleFor(x => x.Description)
            .NotEmpty()
            .MaximumLength(1024);
    }
}

internal sealed class UpdateScoringModelCommandHandler(
    IWaydDbContext waydDbContext,
    ILogger<UpdateScoringModelCommandHandler> logger)
    : ICommandHandler<UpdateScoringModelCommand>
{
    private const string AppRequestName = nameof(UpdateScoringModelCommand);

    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<UpdateScoringModelCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateScoringModelCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _waydDbContext.ScoringModels
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);
            if (model is null)
            {
                _logger.LogInformation("Scoring Model {ScoringModelId} not found.", request.Id);
                return Result.Failure("Scoring Model not found.");
            }

            var updateResult = model.Update(
                request.Name,
                request.Description
                );
            if (updateResult.IsFailure)
            {
                _logger.LogError("Unable to update Scoring Model {ScoringModelId}.  Error message: {Error}", model.Id, updateResult.Error);
                return Result.Failure(updateResult.Error);
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Scoring Model {ScoringModelId} updated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
