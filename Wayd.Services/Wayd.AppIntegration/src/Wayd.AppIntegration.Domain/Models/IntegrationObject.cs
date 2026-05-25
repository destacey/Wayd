using System.Text.Json.Serialization;
using Wayd.Common.Domain.Models;

namespace Wayd.AppIntegration.Domain.Models;

public abstract class IntegrationObject<TId>
{
    [JsonInclude]
    public IntegrationState<TId>? IntegrationState { get; protected set; }

    public bool HasIntegration => IntegrationState is not null;

    public bool IntegrationIsActive => IntegrationState?.IsActive ?? false;

    public Result UpdateIntegrationState(bool isActive)
    {
        if (IntegrationState is null)
            return Result.Failure("Integration state is not set.");

        IntegrationState.SetIsActive(isActive);

        return Result.Success();
    }

    public Result AddIntegrationState(IntegrationState<TId> integrationState)
    {
        if (IntegrationState is not null)
            return Result.Failure("Integration state is already set.");

        IntegrationState = integrationState;

        return Result.Success();
    }

    public Result ReplaceIntegrationState(IntegrationState<TId> integrationState)
    {
        IntegrationState = integrationState;

        return Result.Success();
    }

    public Result RemoveIntegrationState()
    {
        IntegrationState = null;

        return Result.Success();
    }
}
