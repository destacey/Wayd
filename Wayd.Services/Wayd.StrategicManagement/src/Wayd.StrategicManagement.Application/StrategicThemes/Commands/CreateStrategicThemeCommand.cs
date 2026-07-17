using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Enums.StrategicManagement;
using Wayd.StrategicManagement.Domain.Models;

namespace Wayd.StrategicManagement.Application.StrategicThemes.Commands;

public sealed record CreateStrategicThemeCommand(string Name, string Description) : ICommand<ObjectIdAndKey>;

public sealed class CreateStrategicThemeCommandValidator : AbstractValidator<CreateStrategicThemeCommand>
{
    public CreateStrategicThemeCommandValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(64);

        RuleFor(x => x.Description)
            .MaximumLength(1024);
    }
}

public sealed class CreateStrategicThemeCommandHandler(
    IStrategicManagementDbContext strategicManagementDbContext,
    ILogger<CreateStrategicThemeCommandHandler> logger,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<CreateStrategicThemeCommand, ObjectIdAndKey>
{
    private const string AppRequestName = nameof(CreateStrategicThemeCommand);

    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;
    private readonly ILogger<CreateStrategicThemeCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<ObjectIdAndKey>> Handle(CreateStrategicThemeCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var strategicTheme = StrategicTheme.Create(
                request.Name,
                request.Description,
                StrategicThemeState.Proposed,
                _dateTimeProvider.Now
                );

            await _strategicManagementDbContext.StrategicThemes.AddAsync(strategicTheme, cancellationToken);
            await _strategicManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Strategic Theme {StrategicThemeId} created with Key {StrategicThemeKey}.", strategicTheme.Id, strategicTheme.Key);

            return new ObjectIdAndKey(strategicTheme.Id, strategicTheme.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}

