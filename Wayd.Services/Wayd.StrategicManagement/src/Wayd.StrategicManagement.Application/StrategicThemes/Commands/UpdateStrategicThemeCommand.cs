using Wayd.Common.Domain.Events;

namespace Wayd.StrategicManagement.Application.StrategicThemes.Commands;

public sealed record UpdateStrategicThemeCommand(Guid Id, string Name, string Description) : ICommand;

public sealed class UpdateStrategicThemeCommandValidator : AbstractValidator<UpdateStrategicThemeCommand>
{
    public UpdateStrategicThemeCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty();

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.Description)
            .MaximumLength(1024);
    }
}

public sealed class UpdateStrategicThemeCommandHandler(
    IStrategicManagementDbContext strategicManagementDbContext,
    ICurrentUser currentUser,
    ILogger<UpdateStrategicThemeCommandHandler> logger,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateStrategicThemeCommand>
{
    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UpdateStrategicThemeCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UpdateStrategicThemeCommand request, CancellationToken cancellationToken)
    {
        var strategicTheme = await _strategicManagementDbContext.StrategicThemes
            .FirstOrDefaultAsync(r => r.Id == request.Id, cancellationToken);

        if (strategicTheme is null)
        {
            _logger.LogInformation("Strategic Theme {StrategicThemeId} not found.", request.Id);
            return Result.Failure("Strategic Theme not found.");
        }

        var updateResult = strategicTheme.UpdateDetails(
            request.Name,
            request.Description,
            EventActor.User(_currentUser.GetUserId()),
            _dateTimeProvider.Now
            );

        if (updateResult.IsFailure)
        {
            _logger.LogInformation("Unable to update Strategic Theme {StrategicThemeId}.  Error message: {Error}", request.Id, updateResult.Error);
            return Result.Failure(updateResult.Error);
        }

        await _strategicManagementDbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Strategic Theme {StrategicThemeId} updated.", request.Id);

        return Result.Success();
    }
}
