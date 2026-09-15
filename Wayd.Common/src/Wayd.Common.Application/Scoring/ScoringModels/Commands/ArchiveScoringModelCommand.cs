using Wayd.Common.Application.Persistence;
namespace Wayd.Common.Application.Scoring.ScoringModels.Commands;

public sealed record ArchiveScoringModelCommand(Guid Id) : ICommand;

public sealed class ArchiveScoringModelCommandValidator : AbstractValidator<ArchiveScoringModelCommand>
{
    public ArchiveScoringModelCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class ArchiveScoringModelCommandHandler(
    IWaydDbContext waydDbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<ArchiveScoringModelCommandHandler> logger)
    : ICommandHandler<ArchiveScoringModelCommand>
{
    private const string AppRequestName = nameof(ArchiveScoringModelCommand);
    private readonly IWaydDbContext _waydDbContext = waydDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ArchiveScoringModelCommandHandler> _logger = logger;

    public async Task<Result> Handle(ArchiveScoringModelCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var model = await _waydDbContext.ScoringModels
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (model is null)
            {
                _logger.LogInformation("Scoring Model {ScoringModelId} not found.", request.Id);
                return Result.Failure("Scoring Model not found.");
            }

            var archiveResult = model.Archive(EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()), _dateTimeProvider.Now);
            if (archiveResult.IsFailure)
            {
                _logger.LogError("Unable to archive Scoring Model {ScoringModelId}.  Error message: {Error}", request.Id, archiveResult.Error);
                return Result.Failure(archiveResult.Error);
            }

            await _waydDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Scoring Model {ScoringModelId} archived.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
