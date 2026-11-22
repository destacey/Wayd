namespace Wayd.StrategicManagement.Application.StrategicThemes.Commands;

public sealed record ActivateStrategicThemeCommand(Guid Id) : ICommand;

public sealed class ActivateStrategicThemeCommandValidator : AbstractValidator<ActivateStrategicThemeCommand>
{
    public ActivateStrategicThemeCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class ActivateStrategicThemeCommandHandler(IStrategicManagementDbContext strategicManagementDbContext, ICurrentUser currentUser, ILogger<ActivateStrategicThemeCommandHandler> logger, IDateTimeProvider dateTimeProvider) : ICommandHandler<ActivateStrategicThemeCommand>
{
    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<ActivateStrategicThemeCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ActivateStrategicThemeCommand request, CancellationToken cancellationToken)
    {
        var theme = await _strategicManagementDbContext.StrategicThemes
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (theme is null)
        {
            _logger.LogInformation("Strategic Theme {StrategicThemeId} not found.", request.Id);
            return Result.Failure("Strategic Theme not found.");
        }

        var activateResult = theme.Activate(EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);
        if (activateResult.IsFailure)
        {
            _logger.LogInformation("Unable to activate Strategic Theme {StrategicThemeId}.  Error message: {Error}", request.Id, activateResult.Error);
            return Result.Failure(activateResult.Error);
        }
        await _strategicManagementDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Strategic Theme {StrategicThemeId} activated.", request.Id);

        return Result.Success();
    }
}