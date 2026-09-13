using Wayd.Common.Domain.Events;

namespace Wayd.StrategicManagement.Application.StrategicThemes.Commands;

public sealed record DeleteStrategicThemeCommand(Guid Id) : ICommand;
public sealed class DeleteStrategicThemeCommandValidator : AbstractValidator<DeleteStrategicThemeCommand>
{
    public DeleteStrategicThemeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();
    }
}

public sealed class DeleteStrategicThemeCommandHandler(IStrategicManagementDbContext strategicManagementDbContext, ICurrentUser currentUser, ILogger<DeleteStrategicThemeCommandHandler> logger, IDateTimeProvider dateTimeProvider) : ICommandHandler<DeleteStrategicThemeCommand>
{
    private const string AppRequestName = nameof(DeleteStrategicThemeCommand);

    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<DeleteStrategicThemeCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(DeleteStrategicThemeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var strategicTheme = await _strategicManagementDbContext.StrategicThemes
                .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

            if (strategicTheme is null)
            {
                _logger.LogInformation("Strategic Theme {StrategicThemeId} not found.", request.Id);
                return Result.Failure("Strategic Theme not found.");
            }

            var deleteResult = strategicTheme.Delete(EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);
            if (deleteResult.IsFailure)
            {
                _logger.LogInformation("Strategic Theme {StrategicThemeId} cannot be deleted. Error message: {Error}", request.Id, deleteResult.Error);
                return Result.Failure(deleteResult.Error);
            }

            _strategicManagementDbContext.StrategicThemes.Remove(strategicTheme);
            await _strategicManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Strategic Theme {StrategicThemeId} deleted.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
