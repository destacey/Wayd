using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;
using Wayd.AppIntegration.Domain.Models.Entra;

namespace Wayd.AppIntegration.Application.Persistence;

public interface IAppIntegrationDbContext : IWaydDbContext
{
    DbSet<Connection> Connections { get; }
    DbSet<AzureDevOpsBoardsConnection> AzureDevOpsBoardsConnections { get; }
    DbSet<AzureOpenAIConnection> AzureOpenAIConnections { get; }
    DbSet<EntraConnection> EntraConnections { get; }
    DbSet<SyncRun> SyncRuns { get; }
}
