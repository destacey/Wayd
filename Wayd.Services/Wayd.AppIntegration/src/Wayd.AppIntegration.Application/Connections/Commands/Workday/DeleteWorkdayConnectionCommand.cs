using Microsoft.EntityFrameworkCore;

namespace Wayd.AppIntegration.Application.Connections.Commands.Workday;

public sealed record DeleteWorkdayConnectionCommand(Guid Id) : ICommand;

internal sealed class DeleteWorkdayConnectionCommandHandler : ICommandHandler<DeleteWorkdayConnectionCommand>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext;
    private readonly ILogger<DeleteWorkdayConnectionCommandHandler> _logger;

    public DeleteWorkdayConnectionCommandHandler(IAppIntegrationDbContext appIntegrationDbContext, ILogger<DeleteWorkdayConnectionCommandHandler> logger)
    {
        _appIntegrationDbContext = appIntegrationDbContext;
        _logger = logger;
    }

    public async Task<Result> Handle(DeleteWorkdayConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await _appIntegrationDbContext.WorkdayConnections
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (connection is null)
            {
                _logger.LogError("Workday Connection {ConnectionId} not found.", request.Id);
                return Result.Failure($"Workday Connection {request.Id} not found.");
            }

            _appIntegrationDbContext.WorkdayConnections.Remove(connection);
            await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Workday Connection {ConnectionId}.", request.Id);
            return Result.Failure($"Error deleting Workday Connection {request.Id}. {ex.Message}");
        }
    }
}
