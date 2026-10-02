using Microsoft.EntityFrameworkCore;

namespace Wayd.AppIntegration.Application.Connections.Commands.AzureOpenAI;

public sealed record DeleteAzureOpenAIConnectionCommand(Guid Id) : ICommand;

public sealed class DeleteAzureOpenAIConnectionCommandHandler : ICommandHandler<DeleteAzureOpenAIConnectionCommand>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext;
    private readonly ICurrentUser _currentUser;
    private readonly IDateTimeProvider _dateTimeProvider;
    private readonly ILogger<DeleteAzureOpenAIConnectionCommandHandler> _logger;

    public DeleteAzureOpenAIConnectionCommandHandler(IAppIntegrationDbContext appIntegrationDbContext, ICurrentUser currentUser, IDateTimeProvider dateTimeProvider, ILogger<DeleteAzureOpenAIConnectionCommandHandler> logger)
    {
        _appIntegrationDbContext = appIntegrationDbContext;
        _currentUser = currentUser;
        _dateTimeProvider = dateTimeProvider;
        _logger = logger;
    }

    public async Task<Result> Handle(DeleteAzureOpenAIConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await _appIntegrationDbContext.AzureOpenAIConnections
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (connection is null)
            {
                _logger.LogError("Azure OpenAI Connection {ConnectionId} not found.", request.Id);
                return Result.Failure($"Azure OpenAI Connection {request.Id} not found.");
            }

            connection.Delete(EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()), _dateTimeProvider.Now);
            _appIntegrationDbContext.AzureOpenAIConnections.Remove(connection);
            await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting Azure OpenAI Connection {ConnectionId}.", request.Id);
            return Result.Failure($"Error deleting Azure OpenAI Connection {request.Id}. {ex.Message}");
        }
    }
}
