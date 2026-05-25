namespace Wayd.AppIntegration.Application.Connections.Commands;

/// <summary>
/// Activates a connection. Connector-agnostic — operates on the base <see cref="Connection"/>
/// entity. No-op if the connection is already active.
/// </summary>
public sealed record ActivateConnectionCommand(Guid Id) : ICommand;

internal sealed class ActivateConnectionCommandHandler(
    IAppIntegrationDbContext appIntegrationDbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<ActivateConnectionCommandHandler> logger) : ICommandHandler<ActivateConnectionCommand>
{
    private const string AppRequestName = nameof(ActivateConnectionCommandHandler);

    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<ActivateConnectionCommandHandler> _logger = logger;

    public async Task<Result> Handle(ActivateConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await _appIntegrationDbContext.Connections
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (connection is null)
                return Result.Failure($"Connection {request.Id} not found.");

            var result = connection.Activate(_dateTimeProvider.Now);
            if (result.IsFailure)
                return result;

            await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

            return Result.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Wayd Request: Exception for Request {Name} {@Request}", AppRequestName, request);
            return Result.Failure($"Wayd Request: Exception for Request {AppRequestName} {request}");
        }
    }
}
