using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.ProductTags.Commands;

/// <summary>Saves the product tag settings.</summary>
public sealed record UpdateProductTagSettingsCommand(bool AllowsFreeTags) : ICommand;

public sealed class UpdateProductTagSettingsCommandHandler(
    ISystemSettingsStore store,
    ICurrentUser currentUser,
    ILogger<UpdateProductTagSettingsCommandHandler> logger)
    : ICommandHandler<UpdateProductTagSettingsCommand>
{
    private const string AppRequestName = nameof(UpdateProductTagSettingsCommand);

    private readonly ISystemSettingsStore _store = store;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly ILogger<UpdateProductTagSettingsCommandHandler> _logger = logger;

    public async Task<Result> Handle(UpdateProductTagSettingsCommand request, CancellationToken cancellationToken)
    {
        try
        {
            return await _store.Save(
                new ProductTagSettings { AllowsFreeTags = request.AllowsFreeTags },
                EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Exception handling {CommandName} command for request {@Request}.", AppRequestName, request);
            return Result.Failure($"Error handling {AppRequestName} command.");
        }
    }
}
