namespace Wayd.AppIntegration.Application.Connections.Commands;

/// <summary>
/// Deactivates a connection. Connector-agnostic — operates on the base <see cref="Connection"/>
/// entity. Inactive connections are excluded from all sync runs (work and people) since
/// <c>CanSync</c> and the runners' active filters both require <c>IsActive</c>. No-op if the
/// connection is already inactive.
/// </summary>
public sealed record DeactivateConnectionCommand(Guid Id) : ICommand;

internal sealed class DeactivateConnectionCommandHandler(
    IAppIntegrationDbContext appIntegrationDbContext,
    IDateTimeProvider dateTimeProvider,
    ILogger<DeactivateConnectionCommandHandler> logger) : ICommandHandler<DeactivateConnectionCommand>
{
    private const string AppRequestName = nameof(DeactivateConnectionCommandHandler);

    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;
    private readonly ILogger<DeactivateConnectionCommandHandler> _logger = logger;

    public async Task<Result> Handle(DeactivateConnectionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var connection = await _appIntegrationDbContext.Connections
                .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
            if (connection is null)
                return Result.Failure($"Connection {request.Id} not found.");

            var result = connection.Deactivate(_dateTimeProvider.Now);
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
