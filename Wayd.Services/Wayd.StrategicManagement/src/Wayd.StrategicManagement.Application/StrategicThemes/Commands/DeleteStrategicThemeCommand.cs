using Wayd.Common.Application.Events;
using Wayd.Common.Application.Models;
using Wayd.Common.Domain.Events.StrategicManagement;

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

public sealed class DeleteStrategicThemeCommandHandler(IStrategicManagementDbContext strategicManagementDbContext, ILogger<DeleteStrategicThemeCommandHandler> logger, IDateTimeProvider dateTimeProvider, IEventPublisher eventPublisher) : ICommandHandler<DeleteStrategicThemeCommand>
{
    private const string AppRequestName = nameof(DeleteStrategicThemeCommand);

    private readonly IStrategicManagementDbContext _strategicManagementDbContext = strategicManagementDbContext;
    private readonly ILogger<DeleteStrategicThemeCommandHandler> _logger = logger;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly IEventPublisher _eventPublisher = eventPublisher;

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

            if (!strategicTheme.CanBeDeleted())
            {
                _logger.LogInformation("Strategic Theme {StrategicThemeId} cannot be deleted.", request.Id);
                return Result.Failure("Strategic Theme cannot be deleted.");
            }

            _strategicManagementDbContext.StrategicThemes.Remove(strategicTheme);
            await _strategicManagementDbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Strategic Theme {StrategicThemeId} deleted.", request.Id);

            var deleteEvent = new StrategicThemeDeletedEvent(request.Id, _dateTimeProvider.Now);
            await _eventPublisher.PublishAsync(deleteEvent);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure<ObjectIdAndKey>($"Error handling {AppRequestName} command.");
        }
    }
}
