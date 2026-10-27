using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeChannels.Commands;

/// <summary>
/// Brings an archived channel back into use. Desks offering every channel offer it again, as do desks that
/// had chosen it before it was archived. The Administer permission is checked by the endpoint.
/// </summary>
/// <param name="Id">The channel.</param>
public sealed record RestoreIntakeChannelCommand(Guid Id) : ICommand;

public sealed class RestoreIntakeChannelCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<RestoreIntakeChannelCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(RestoreIntakeChannelCommand request, CancellationToken cancellationToken)
    {
        var channel = await _dbContext.IntakeChannels.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (channel is null)
            return Result.Failure(IntakeChannelErrors.NotFoundError);

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var result = channel.Restore(EventActor.User(_currentUser.GetUserId(), employeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
