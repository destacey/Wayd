using Microsoft.EntityFrameworkCore;
using Wayd.Common.Domain.Models;

namespace Wayd.AppIntegration.Application.Connections.Commands;

public sealed record UpdateAzureDevOpsWorkProcessIntegrationStateCommand(Guid ConnectionId, IntegrationRegistration<Guid, Guid> IntegrationRegistration) : ICommand;

public sealed class UpdateAzureDevOpsBoardsWorkProcessIntegrationStateCommandHandler : ICommandHandler<UpdateAzureDevOpsWorkProcessIntegrationStateCommand>
{
    private const string AppRequestName = nameof(UpdateAzureDevOpsWorkProcessIntegrationStateCommand);

    private readonly IAppIntegrationDbContext _appIntegrationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<UpdateAzureDevOpsBoardsWorkProcessIntegrationStateCommandHandler> _logger;

    public UpdateAzureDevOpsBoardsWorkProcessIntegrationStateCommandHandler(IAppIntegrationDbContext appIntegrationDbContext, IDateTimeProvider dateTimeProvider, ILogger<UpdateAzureDevOpsBoardsWorkProcessIntegrationStateCommandHandler> logger)
    {
        _appIntegrationDbContext = appIntegrationDbContext;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result> Handle(UpdateAzureDevOpsWorkProcessIntegrationStateCommand request, CancellationToken cancellationToken)
    {
        var connection = await _appIntegrationDbContext.AzureDevOpsBoardsConnections.FirstOrDefaultAsync(c => c.Id == request.ConnectionId, cancellationToken);
        if (connection is null)
        {
            _logger.LogError("Unable to find Azure DevOps connection with id {ConnectionId}.", request.ConnectionId);
            return Result.Failure($"Unable to find Azure DevOps connection with id {request.ConnectionId}.");
        }

        var updateWorkProcessesResult = connection.UpdateWorkProcessIntegrationState(request.IntegrationRegistration, _dateTimeProvider.Now);
        if (updateWorkProcessesResult.IsFailure)
        {
            _logger.LogError("Errors occurred while processing {AppRequestName}. {Error}", AppRequestName, updateWorkProcessesResult.Error);

            return Result.Failure($"Errors occurred while processing {AppRequestName}.");
        }

        await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
