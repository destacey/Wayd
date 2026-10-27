using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeChannels.Commands;

/// <summary>
/// Takes a channel out of use: no new request can be given it and no desk offers it, while requests already
/// carrying it keep it. Channels are archived rather than deleted, because requests refer to them by id. The
/// Administer permission is checked by the endpoint.
/// </summary>
/// <param name="Id">The channel.</param>
public sealed record ArchiveIntakeChannelCommand(Guid Id) : ICommand;

public sealed class ArchiveIntakeChannelCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ArchiveIntakeChannelCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ArchiveIntakeChannelCommand request, CancellationToken cancellationToken)
    {
        var channel = await _dbContext.IntakeChannels.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (channel is null)
            return Result.Failure(IntakeChannelErrors.NotFoundError);

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var result = channel.Archive(EventActor.User(_currentUser.GetUserId(), employeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
