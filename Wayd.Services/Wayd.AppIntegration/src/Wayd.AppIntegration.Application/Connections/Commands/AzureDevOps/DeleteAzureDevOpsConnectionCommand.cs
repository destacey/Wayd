using Microsoft.EntityFrameworkCore;

namespace Wayd.AppIntegration.Application.Connections.Commands.AzureDevOps;

public sealed record DeleteAzureDevOpsConnectionCommand(Guid Id) : ICommand;

public sealed class DeleteAzureDevOpsConnectionCommandHandler(IAppIntegrationDbContext appIntegrationDbContext, ILogger<DeleteAzureDevOpsConnectionCommandHandler> logger) : ICommandHandler<DeleteAzureDevOpsConnectionCommand>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly ILogger<DeleteAzureDevOpsConnectionCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteAzureDevOpsConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await _appIntegrationDbContext.AzureDevOpsBoardsConnections
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (connection is null)
            {
                _logger.LogError("Azure DevOps Connection {ConnectionId} not found.", request.Id);
                return Result.Failure($"Azure DevOps Connection {request.Id} not found.");
            }

            _appIntegrationDbContext.AzureDevOpsBoardsConnections.Remove(connection);
            await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Azure DevOps Connection {ConnectionId}.", request.Id);
            return Result.Failure($"Error deleting Azure DevOps Connection {request.Id}. {ex.Message}");
        }
    }
}
