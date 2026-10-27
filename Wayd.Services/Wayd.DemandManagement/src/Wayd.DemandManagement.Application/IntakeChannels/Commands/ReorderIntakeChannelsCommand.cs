using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeChannels.Commands;

/// <summary>
/// Puts the channels in the order requesters see them. The Administer permission is checked by the endpoint.
/// </summary>
/// <param name="OrderedChannelIds">
/// Every channel, archived ones included, in the order they should be presented. The whole list, not a
/// subset: position is relative, so naming only some would leave the rest at stale positions and could put
/// two channels in the same place.
/// </param>
public sealed record ReorderIntakeChannelsCommand(IReadOnlyList<Guid> OrderedChannelIds) : ICommand;

public sealed class ReorderIntakeChannelsCommandValidator : CustomValidator<ReorderIntakeChannelsCommand>
{
    public ReorderIntakeChannelsCommandValidator()
    {
        RuleFor(c => c.OrderedChannelIds)
            .NotEmpty()
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithMessage("The order cannot contain an empty id.")
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("The order cannot name the same channel twice.");
    }
}

public sealed class ReorderIntakeChannelsCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<ReorderIntakeChannelsCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(ReorderIntakeChannelsCommand request, CancellationToken cancellationToken)
    {
        var channelsById = await _dbContext.IntakeChannels.ToDictionaryAsync(c => c.Id, cancellationToken);
        var orderedIds = request.OrderedChannelIds;

        // A caller working from a filtered or stale list would otherwise renumber part of the set and leave
        // the rest overlapping it. Checked in full before anything moves.
        if (orderedIds.Count != channelsById.Count
            || !orderedIds.All(channelsById.ContainsKey)
            || orderedIds.Distinct().Count() != orderedIds.Count)
        {
            return Result.Failure("The order must list every channel exactly once.");
        }

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var actor = EventActor.User(_currentUser.GetUserId(), employeeId);
        var now = _dateTimeProvider.Now;
        for (var position = 0; position < orderedIds.Count; position++)
        {
            channelsById[orderedIds[position]].Reorder(position + 1, actor, now);
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
