using Microsoft.EntityFrameworkCore;

namespace Wayd.AppIntegration.Application.Connections.Commands.Workday;

public sealed record DeleteWorkdayConnectionCommand(Guid Id) : ICommand;

public sealed class DeleteWorkdayConnectionCommandHandler : ICommandHandler<DeleteWorkdayConnectionCommand>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<DeleteWorkdayConnectionCommandHandler> _logger;

    public DeleteWorkdayConnectionCommandHandler(IAppIntegrationDbContext appIntegrationDbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, ILogger<DeleteWorkdayConnectionCommandHandler> logger)
    {
        _appIntegrationDbContext = appIntegrationDbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
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

            connection.Delete(EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()), _dateTimeProvider.Now);
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
