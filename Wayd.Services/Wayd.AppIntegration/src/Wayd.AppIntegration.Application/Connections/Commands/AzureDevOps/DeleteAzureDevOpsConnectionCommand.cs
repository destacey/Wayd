using Microsoft.EntityFrameworkCore;

namespace Wayd.AppIntegration.Application.Connections.Commands.AzureDevOps;

public sealed record DeleteAzureDevOpsConnectionCommand(Guid Id) : ICommand;

public sealed class DeleteAzureDevOpsConnectionCommandHandler(IAppIntegrationDbContext appIntegrationDbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, ILogger<DeleteAzureDevOpsConnectionCommandHandler> logger) : ICommandHandler<DeleteAzureDevOpsConnectionCommand>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<DeleteAzureDevOpsConnectionCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeleteAzureDevOpsConnectionCommand request, CancellationToken cancellationToken)
    {
        var connection = await _appIntegrationDbContext.AzureDevOpsBoardsConnections
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (connection is null)
        {
            _logger.LogError("Azure DevOps Connection {ConnectionId} not found.", request.Id);
            return Result.Failure($"Azure DevOps Connection {request.Id} not found.");
        }

        connection.Delete(EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()), _dateTimeProvider.Now);
        _appIntegrationDbContext.AzureDevOpsBoardsConnections.Remove(connection);
        await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
