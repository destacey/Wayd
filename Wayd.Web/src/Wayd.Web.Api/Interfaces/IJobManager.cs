using Wayd.AppIntegration.Domain.Models;
using Wayd.Common.Application.Enums;

namespace Wayd.Web.Api.Interfaces;

public interface IJobManager
{
    Task RunPeopleSync(SyncType syncType, SyncTriggerSource trigger, Guid? connectionId, CancellationToken cancellationToken);
    Task RunWorkSync(SyncType syncType, SyncTriggerSource trigger, Guid? connectionId, CancellationToken cancellationToken);
    Task RunSyncTeamsWithGraphTables(CancellationToken cancellationToken);
    Task RunSyncIterations(CancellationToken cancellationToken);
    Task RunSyncStrategicThemes(CancellationToken cancellationToken);
    Task RunSyncProjects(CancellationToken cancellationToken);
    Task RunSyncTeams(CancellationToken cancellationToken);
    Task RunPortfolioRankRebalance(CancellationToken cancellationToken);

    /// <summary>
    /// Repoints work items attributed to one external identity after an admin maps or ignores it.
    /// </summary>
    Task RunRepointWorkItemAttribution(string externalId, Guid? employeeId, CancellationToken cancellationToken);
}
