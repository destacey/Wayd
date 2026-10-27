using Wayd.Common.Domain.Events;
using Wayd.DemandManagement.Domain.Models;

namespace Wayd.DemandManagement.Application.IntakeChannels.Commands;

/// <summary>
/// Adds a channel to the end of the company's list. The Administer permission is checked by the endpoint.
/// </summary>
/// <remarks>
/// Desks offering every channel offer it at once; desks that chose their channels do not, until a Desk Admin
/// adds it.
/// </remarks>
/// <param name="Name">What the company calls the channel. Unique among active and archived channels.</param>
/// <param name="Description">What belongs in the channel, shown to whoever picks one.</param>
public sealed record CreateIntakeChannelCommand(string Name, string? Description) : ICommand<Guid>;

public sealed class CreateIntakeChannelCommandValidator : CustomValidator<CreateIntakeChannelCommand>
{
    public CreateIntakeChannelCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(64);
        RuleFor(c => c.Description).MaximumLength(512);
    }
}

public sealed class CreateIntakeChannelCommandHandler(
    IDemandManagementDbContext dbContext,
    ICurrentPrincipal currentPrincipal,
    ICurrentUser currentUser,
    IDateTimeProvider dateTimeProvider)
    : ICommandHandler<CreateIntakeChannelCommand, Guid>
{
    private readonly IDemandManagementDbContext _dbContext = dbContext;
    private readonly ICurrentPrincipal _currentPrincipal = currentPrincipal;
    private readonly ICurrentUser _currentUser = currentUser;
    private readonly IDateTimeProvider _dateTimeProvider = dateTimeProvider;

    public async Task<Result<Guid>> Handle(CreateIntakeChannelCommand request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        if (await _dbContext.IntakeChannels.AnyAsync(c => c.Name == name, cancellationToken))
            return Result.Failure<Guid>(IntakeChannelErrors.DuplicateNameError);

        var lastOrder = await _dbContext.IntakeChannels.MaxAsync(c => (int?)c.Order, cancellationToken) ?? 0;

        var employeeId = await _currentPrincipal.GetEmployeeId(cancellationToken);
        var channel = IntakeChannel.Create(name, request.Description, lastOrder + 1,
            EventActor.User(_currentUser.GetUserId(), employeeId), _dateTimeProvider.Now);

        _dbContext.IntakeChannels.Add(channel);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return channel.Id;
    }
}
