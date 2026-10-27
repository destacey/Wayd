using Wayd.Common.Domain.Events;

namespace Wayd.DemandManagement.Application.IntakeChannels.Commands;

/// <summary>
/// Renames a channel or changes what it says belongs in it. Every request carrying the channel reads the new
/// name. The Administer permission is checked by the endpoint.
/// </summary>
/// <param name="Id">The channel.</param>
/// <param name="Name">What the company calls the channel. Unique among active and archived channels.</param>
/// <param name="Description">What belongs in the channel, or null for no description.</param>
public sealed record UpdateIntakeChannelCommand(Guid Id, string Name, string? Description) : ICommand;

public sealed class UpdateIntakeChannelCommandValidator : CustomValidator<UpdateIntakeChannelCommand>
{
    public UpdateIntakeChannelCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().MaximumLength(64);
        RuleFor(c => c.Description).MaximumLength(512);
    }
}

public sealed class UpdateIntakeChannelCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<UpdateIntakeChannelCommand>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result> Handle(UpdateIntakeChannelCommand request, CancellationToken cancellationToken)
    {
        var channel = await _dbContext.IntakeChannels.FirstOrDefaultAsync(c => c.Id == request.Id, cancellationToken);
        if (channel is null)
            return Result.Failure(IntakeChannelErrors.NotFoundError);

        var name = request.Name.Trim();
        if (await _dbContext.IntakeChannels.AnyAsync(c => c.Id != channel.Id && c.Name == name, cancellationToken))
            return Result.Failure(IntakeChannelErrors.DuplicateNameError);

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var result = channel.UpdateDetails(name, request.Description,
            EventActor.User(_currentUser.GetUserId(), employeeId), _dateTimeProvider.Now);
        if (result.IsFailure)
            return result;

        await _dbContext.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
