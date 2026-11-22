namespace Wayd.StrategicManagement.Application.StrategicThemes.Commands;

public sealed record ArchiveStrategicThemeCommand(Guid Id) : ICommand;

public sealed class ArchiveStrategicThemeCommandValidator : AbstractValidator<ArchiveStrategicThemeCommand>
{
    public ArchiveStrategicThemeCommandValidator()
    {
        RuleFor(v => v.Id)
            .NotEmpty();
    }
}

public sealed class ArchiveStrategicThemeCommandHandler(IStrategicManagementDbContext strategicManagementDbContext, ICurrentUser currentUser, ILogger<ArchiveStrategicThemeCommandHandler> logger, IDateTimeProvider dateTimeProvider) : ICommandHandler<ArchiveStrategicThemeCommand>
{
    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<ArchiveStrategicThemeCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ArchiveStrategicThemeCommand request, CancellationToken cancellationToken)
    {
        var theme = await _strategicManagementDbContext.StrategicThemes
            .FirstOrDefaultAsync(s => s.Id == request.Id, cancellationToken);
        if (theme is null)
        {
            _logger.LogInformation("Strategic Theme {StrategicThemeId} not found.", request.Id);
            return Result.Failure("Strategic Theme not found.");
        }

        var archiveResult = theme.Archive(EventActor.User(_currentUser.GetUserId()), _dateTimeProvider.Now);
        if (archiveResult.IsFailure)
        {
            _logger.LogInformation("Unable to archive Strategic Theme {StrategicThemeId}.  Error message: {Error}", request.Id, archiveResult.Error);
            return Result.Failure(archiveResult.Error);
        }
        await _strategicManagementDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Strategic Theme {StrategicThemeId} archived.", request.Id);

        return Result.Success();
    }
}