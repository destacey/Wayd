namespace Wayd.AppIntegration.Application.Connections.Commands;

/// <summary>
/// Deactivates a connection. Connector-agnostic — operates on the base <see cref="Connection"/>
/// entity. Inactive connections are excluded from all sync runs (work and people) since
/// <c>CanSync</c> and the runners' active filters both require <c>IsActive</c>. No-op if the
/// connection is already inactive.
/// </summary>
public sealed record DeactivateConnectionCommand(Guid Id) : ICommand;

public sealed class DeactivateConnectionCommandHandler(
    IAppIntegrationDbContext appIntegrationDbContext,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider) : ICommandHandler<DeactivateConnectionCommand>
{
    private readonly IAppIntegrationDbContext _appIntegrationDbContext = appIntegrationDbContext;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(DeactivateConnectionCommand request, CancellationToken cancellationToken)
    {
        var connection = await _appIntegrationDbContext.Connections
            .FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (connection is null)
            return Result.Failure($"Connection {request.Id} not found.");

        var result = connection.Deactivate(ConnectionActivatableArgs.Create(EventActor.User(_currentUser.GetUserId(), _currentUser.GetEmployeeId()), _dateTimeProvider.Now));
        if (result.IsFailure)
            return result;

        await _appIntegrationDbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
