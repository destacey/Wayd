using Wayd.Common.Domain.Settings;

namespace Wayd.Common.Application.SystemSettings.ProductTags.Commands;

/// <summary>Saves the product tag settings.</summary>
public sealed record UpdateProductTagSettingsCommand(bool AllowsFreeTags) : ICommand;

public sealed class UpdateProductTagSettingsCommandHandler(
    ISystemSettingsStore store,
    ICurrentUser currentUser)
    : ICommandHandler<UpdateProductTagSettingsCommand>
{
    private readonly ISystemSettingsStore _store = store;
    private readonly ICurrentUser _currentUser = currentUser;

    public async Task<Result> Handle(UpdateProductTagSettingsCommand request, CancellationToken cancellationToken)
    {
        return await _store.Save(
            new ProductTagSettings { AllowsFreeTags = request.AllowsFreeTags },
            EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()),
            cancellationToken);
    }
}
