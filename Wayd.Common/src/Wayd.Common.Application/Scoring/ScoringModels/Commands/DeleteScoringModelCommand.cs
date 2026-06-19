using Wayd.Common.Application.Persistence;
namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record DeleteScoringModelCommand(Guid Id) : ICommand;

public sealed class DeleteScoringModelCommandValidator : AbstractValidator<DeleteScoringModelCommand>
{
    public DeleteScoringModelCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

internal sealed class DeleteScoringModelCommandHandler(
    IWaydDbContext waydDbContext,
    ILogger<DeleteScoringModelCommandHandler> logger)
    : ICommandHandler<DeleteScoringModelCommand>
{
    private const string AppRequestName = nameof(DeleteScoringModelCommand);
    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ILogger<DeleteScoringModelCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteScoringModelCommand request, CancellationToken cancellationToken)
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

            if (!model.CanBeDeleted())
            {
                _logger.LogInformation("Scoring Model {ScoringModelId} cannot be deleted.", request.Id);
                return Result.Failure("Scoring Model cannot be deleted.");
            }

            _waydDbContext.ScoringModels.Remove(model);
            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Scoring Model {ScoringModelId} deleted.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
