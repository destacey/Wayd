using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Application.Enums;

namespace Wayd.Web.Api.Interfaces;

public interface IJobManager
{
    Task RunSyncExternalEmployees(CancellationToken cancellationToken);
    Task RunWorkSync(SyncType syncType, SyncTriggerSource trigger, Guid? connectionId, CancellationToken cancellationToken);
    Task RunSyncTeamsWithGraphTables(CancellationToken cancellationToken);
    Task RunSyncIterations(CancellationToken cancellationToken);
    Task RunSyncStrategicThemes(CancellationToken cancellationToken);
    Task RunSyncProjects(CancellationToken cancellationToken);
    Task RunSyncTeams(CancellationToken cancellationToken);
}
