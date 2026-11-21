using Microsoft.EntityFrameworkCore;

namespace Wayd.AppIntegration.Application.Connections.Commands.Teams;

public sealed record DeleteTeamsConnectionCommand(Guid Id) : ICommand;

public sealed class DeleteTeamsConnectionCommandHandler(
    IAppIntegrationDbContext appIntegrationDbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider,
    ILogger<DeleteTeamsConnectionCommandHandler> logger)
    : ICommandHandler<DeleteTeamsConnectionCommand>
{
    public async Task<Result> Handle(DeleteTeamsConnectionCommand request, CancellationToken cancellationToken)
    {
        var connection = await appIntegrationDbContext.TeamsConnections
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (connection is null)
        {
            logger.LogError("Teams connection {ConnectionId} not found.", request.Id);
            return Result.Failure($"Teams connection {request.Id} not found.");
        }

        connection.Delete(EventActor.User(currentUser.GetUserId(), currentUser.GetEmployeeId()), dateTimeProvider.Now);
        appIntegrationDbContext.TeamsConnections.Remove(connection);
        await appIntegrationDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
