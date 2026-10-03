using Microsoft.EntityFrameworkCore;

namespace Wayd.AppIntegration.Application.Connections.Commands.Entra;

public sealed record DeleteEntraConnectionCommand(Guid Id) : ICommand;

public sealed class DeleteEntraConnectionCommandHandler : ICommandHandler<DeleteEntraConnectionCommand>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<DeleteEntraConnectionCommandHandler> _logger;

    public DeleteEntraConnectionCommandHandler(IAppIntegrationDbContext appIntegrationDbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, ILogger<DeleteEntraConnectionCommandHandler> logger)
    {
        _appIntegrationDbContext = appIntegrationDbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result> Handle(DeleteEntraConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await _appIntegrationDbContext.EntraConnections
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (connection is null)
            {
                _logger.LogError("Entra Connection {ConnectionId} not found.", request.Id);
                return Result.Failure($"Entra Connection {request.Id} not found.");
            }

            connection.Delete(EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()), _dateTimeProvider.Now);
            _appIntegrationDbContext.EntraConnections.Remove(connection);
            await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Entra Connection {ConnectionId}.", request.Id);
            return Result.Failure($"Error deleting Entra Connection {request.Id}. {ex.Message}");
        }
    }
}
