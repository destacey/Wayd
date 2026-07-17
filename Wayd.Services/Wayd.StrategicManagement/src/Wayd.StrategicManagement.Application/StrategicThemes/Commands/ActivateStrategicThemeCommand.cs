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

public sealed class ActivateStrategicThemeCommandHandler(IStrategicManagementDbContext strategicManagementDbContext, ILogger<ActivateStrategicThemeCommandHandler> logger, IDateTimeProvider dateTimeProvider) : ICommandHandler<ActivateStrategicThemeCommand>
{
    private const string AppRequestName = nameof(ActivateStrategicThemeCommand);

    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;
    private readonly ILogger<ActivateStrategicThemeCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ActivateStrategicThemeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var theme = await _strategicManagementDbContext.StrategicThemes
                .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
            if (theme is null)
            {
                _logger.LogInformation("Strategic Theme {StrategicThemeId} not found.", request.Id);
                return Result.Failure("Strategic Theme not found.");
            }

            var activateResult = theme.Activate(_dateTimeProvider.Now);
            if (activateResult.IsFailure)
            {
                // Reset the entity
                await _strategicManagementDbContext.Entry(theme).ReloadAsync(cancellationToken);
                theme.ClearDomainEvents();

                _logger.LogError("Unable to activate Strategic Theme {StrategicThemeId}.  Error message: {Error}", request.Id, activateResult.Error);
                return Result.Failure(activateResult.Error);
            }
            await _strategicManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Strategic Theme {StrategicThemeId} activated.", request.Id);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}