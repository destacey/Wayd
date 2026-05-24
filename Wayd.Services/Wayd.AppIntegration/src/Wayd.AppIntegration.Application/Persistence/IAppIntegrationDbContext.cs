using Microsoft.EntityFrameworkCore;
using Wayd.AppIntegration.Domain.Models.AzureOpenAI;

namespace Wayd.AppIntegration.Application.Persistence;

public interface IAppIntegrationDbContext : IWaydDbContext
{
    DbSet<Connection> Connections { get; }
    DbSet<AzureDevOpsBoardsConnection> AzureDevOpsBoardsConnections { get; }
    DbSet<AzureOpenAIConnection> AzureOpenAIConnections { get; }
    DbSet<SyncRun> SyncRuns { get; }
}
