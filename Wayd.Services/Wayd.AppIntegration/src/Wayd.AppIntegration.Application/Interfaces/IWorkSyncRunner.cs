using Wayd.Common.Application.Enums;

namespace Wayd.AppIntegration.Application.Interfaces;

public interface IWorkSyncRunner : ITransientService
{
    Task<Result> Run(SyncType syncType, SyncTriggerSource trigger, CancellationToken cancellationToken);
    Task<Result> Run(Guid connectionId, SyncType syncType, SyncTriggerSource trigger, CancellationToken cancellationToken);
}
